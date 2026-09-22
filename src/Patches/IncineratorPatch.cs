using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using QuantumObliterator.Net;
using QuantumObliterator.Util;
using UnityEngine;

namespace QuantumObliterator.Patches
{
    /// <summary>
    ///     Diverts a tagged Obliterator's lever pull from "destroy the contents" to
    ///     "ship the contents to the matching Obliterator".
    /// </summary>
    /// <remarks>
    ///     Vanilla flow: the lever calls Incinerator.OnIncinerate, which checks PrivateArea
    ///     access and then does m_nview.InvokeRPC("RPC_RequestIncinerate", playerID). That RPC
    ///     runs on the ZDO OWNER -- which is not necessarily the player who pulled the lever.
    ///     RPC_RequestIncinerate is therefore the right patch point: it already runs on the
    ///     owner and has already rejected the in-use and empty cases.
    ///
    ///     Because the owner and the puller can differ, the user-facing reply is routed back
    ///     with m_nview.InvokeRPC(uid, "QO_Respons", ...) -- exactly the mechanism vanilla uses
    ///     for RPC_IncinerateRespons, so self-delivery and remote delivery both just work.
    /// </remarks>
    [HarmonyPatch]
    internal static class IncineratorPatch
    {
        private const string ResponsRpc = "QO_Respons";

        /// <summary>How long the owner waits for the server before giving up.</summary>
        private const float RequestTimeoutSeconds = 10f;

        /// <summary>Source containers with a transfer in flight; their ZDOIDs are blocked from being opened.</summary>
        internal static readonly HashSet<ZDOID> InFlightContainers = new HashSet<ZDOID>();

        // ------------------------------------------------------------------ registration

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Incinerator), "Awake")]
        private static void Awake_Postfix(Incinerator __instance)
        {
            var nview = __instance.m_nview;
            if (nview == null) return;

            nview.Register<int, string, int>(ResponsRpc,
                (sender, result, detail, amount) => ShowMessage((QOResult)result, detail, amount));
        }

        // ------------------------------------------------------------------ the divert

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Incinerator), "RPC_RequestIncinerate")]
        private static bool RPC_RequestIncinerate_Prefix(Incinerator __instance, long uid, long playerID)
        {
            if (!QOConfig.Enabled.Value) return true;

            var nview = __instance.m_nview;
            if (nview == null || !nview.IsValid()) return true;

            // Vanilla logs and bails in this case; let it.
            if (!nview.IsOwner()) return true;

            var container = __instance.m_container;
            if (container == null) return true;

            // Let vanilla answer Fail for in-use and Empty for empty, so those messages stay familiar.
            if (container.IsInUse() || __instance.isInUse) return true;

            var inventory = container.GetInventory();
            if (inventory == null || inventory.NrOfItems() == 0) return true;

            // The whole transfer reads and writes items through the container's ZDO, and the
            // destination side can only reach a destination Obliterator's items via the ZDO that
            // ObliteratorIndex found by the "incinerator" prefab hash. That only lines up if the
            // Container shares the Incinerator's ZDO -- which it does when the Container sits on
            // the piece root, or points at it via m_rootObjectOverride. Verify rather than assume:
            // if a future prefab ever splits them, fall back to vanilla instead of writing items
            // into the wrong ZDO.
            var containerNview = container.m_nview;
            if (containerNview == null || !containerNview.IsValid())
            {
                return true;
            }

            var containerZdoid = containerNview.GetZDO().m_uid;
            if (containerZdoid != nview.GetZDO().m_uid)
            {
                WarnSplitZdoOnce();
                return true;
            }

            var tag = SignTags.ResolveLocal(__instance.transform.position);
            if (tag.Length == 0)
            {
                QOLog.Debug("Obliterator has no sign nearby; obliterating as vanilla.");
                return true; // untagged -> vanilla behaviour, exactly as before
            }

            QOLog.Debug($"Obliterator tagged '{tag}' pulled by peer {uid}; requesting transfer.");

            __instance.isInUse = true;

            InFlightContainers.Add(containerZdoid);

            __instance.StartCoroutine(TransferRoutine(__instance, uid, containerZdoid, inventory.NrOfItems()));
            return false; // skip vanilla obliteration entirely
        }

        private static bool _warnedSplitZdo;

        private static void WarnSplitZdoOnce()
        {
            if (_warnedSplitZdo) return;
            _warnedSplitZdo = true;
            QOLog.Error(
                "The Obliterator's Container does not share the piece's ZDO. QuantumObliterator " +
                "cannot address the destination's inventory safely in that layout, so every " +
                "Obliterator will fall back to vanilla behaviour. Please report this.");
        }

        private static IEnumerator TransferRoutine(Incinerator inc, long uid, ZDOID containerZdoid, int expectedStacks)
        {
            var nview = inc.m_nview;

            // Mirror vanilla's presentation so a shipment still feels like an obliteration.
            nview.InvokeRPC(ZNetView.Everybody, "RPC_AnimateLever");
            inc.m_leverEffects?.Create(inc.transform.position, inc.transform.rotation, null, 1f, -1);

            yield return new WaitForSeconds(Random.Range(inc.m_effectDelayMin, inc.m_effectDelayMax));

            nview.InvokeRPC(ZNetView.Everybody, "RPC_AnimateLeverReturn");

            var pending = QORpc.BeginRequest(containerZdoid, expectedStacks);

            var deadline = Time.realtimeSinceStartup + RequestTimeoutSeconds;
            while (!pending.Done && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            var outcome = pending.Done
                ? pending.Outcome
                : TransferOutcome.Fail(QOResult.Timeout);

            if (!pending.Done)
            {
                // Stop a late reply from resolving a request nobody is waiting on any more.
                QORpc.Abandon(containerZdoid);
                QOLog.Warn($"Transfer request for {containerZdoid} timed out after {RequestTimeoutSeconds}s.");
            }

            if (outcome.Result == QOResult.Success)
            {
                // The vanilla lightning, at the sending end.
                PlayLightning(inc);
            }

            // The source container is NOT cleared here. The server already wrote an empty item
            // blob to its ZDO, and ZDOPeer.ShouldSend is purely revision-based with no ownership
            // check, so the update reaches this client even though it owns the ZDO;
            // Container.CheckForChanges then reloads it. Clearing locally as well would race
            // that reload for no benefit.

            if (nview.IsValid())
            {
                nview.InvokeRPC(uid, ResponsRpc, (int)outcome.Result, outcome.Detail ?? string.Empty, outcome.Amount);
            }

            InFlightContainers.Remove(containerZdoid);
            inc.isInUse = false;
        }

        // ------------------------------------------------------------------ effects

        /// <summary>Play the lightning at a receiving Obliterator, if it happens to be loaded here.</summary>
        internal static void PlayArrivalEffect(ZDOID destination)
        {
            if (destination == ZDOID.None) return;

            // A dedicated server has no one to show it to.
            if (ZNet.instance != null && ZNet.instance.IsDedicated()) return;

            var scene = ZNetScene.instance;
            if (scene == null) return;

            var go = scene.FindInstance(destination);
            if (go == null) return; // unloaded on this client: nothing to show, and nothing to do

            var inc = go.GetComponent<Incinerator>();
            if (inc != null) PlayLightning(inc);
        }

        private static void PlayLightning(Incinerator inc)
        {
            if (inc.m_lightingAOEs == null) return;

            // Deliberately NOT Object.Instantiate: the raw prefab's Aoe components damage nearby
            // build pieces, which destroys the very sign that tags this Obliterator. A destroyed
            // sign silently unlinks the pair, and the next pull would obliterate the cargo for
            // real. Vanilla obliteration keeps its damaging lightning; shipping does not.
            HarmlessLightning.Spawn(inc.m_lightingAOEs, inc.transform.position, inc.transform.rotation);

            // No Invoke("StopAOE", 4f) here. Despite the name, StopAOE's entire body is
            // `isInUse = false` -- it is vanilla's lever unlock, not an effect teardown. The
            // transfer coroutine owns that flag, and a queued invoke from an earlier pull would
            // clear it mid-way through a later one, letting two transfers overlap.
        }

        // ------------------------------------------------------------------ messaging

        private static void ShowMessage(QOResult result, string detail, int amount)
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            var loc = Localization.instance;
            var text = loc != null ? loc.Localize(QOTokens.For(result)) : QOTokens.For(result);

            if (!string.IsNullOrEmpty(detail))
            {
                // Fuel messages carry an item localisation token; tag messages carry literal text.
                var rendered = detail.StartsWith("$") && loc != null ? loc.Localize(detail) : detail;
                text = text.Replace("{tag}", rendered).Replace("{item}", rendered);
            }

            text = text.Replace("{n}", amount.ToString());

            player.Message(MessageHud.MessageType.Center, text, 0, null, false);
        }
    }
}
