using HarmonyLib;

namespace QuantumObliterator.Patches
{
    /// <summary>
    ///     Blocks opening an Obliterator's container while a transfer is in flight.
    /// </summary>
    /// <remarks>
    ///     The server reads its own synced copy of the source inventory. If a player opened the
    ///     container during the ~1s lever animation and moved something, the server's snapshot
    ///     and the client's view would disagree -- caught by the desync guard in TransferService,
    ///     but only as a failed transfer. Blocking the interaction avoids the situation entirely.
    ///
    ///     Container.m_inUse is not networked, so this only guards the machine running the
    ///     transfer coroutine. That is the owner, which is also the only client whose
    ///     Container.Save could write to the ZDO.
    /// </remarks>
    [HarmonyPatch(typeof(Container), nameof(Container.Interact))]
    internal static class ContainerGuardPatch
    {
        private static bool Prefix(Container __instance, ref bool __result)
        {
            if (IncineratorPatch.InFlightContainers.Count == 0) return true;

            var nview = __instance.m_nview;
            if (nview == null || !nview.IsValid()) return true;

            if (!IncineratorPatch.InFlightContainers.Contains(nview.GetZDO().m_uid)) return true;

            __result = false;
            return false;
        }
    }
}
