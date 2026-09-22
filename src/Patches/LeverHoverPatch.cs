using System.Collections.Generic;
using HarmonyLib;
using QuantumObliterator.Util;
using UnityEngine;

namespace QuantumObliterator.Patches
{
    /// <summary>
    ///     Appends the shipping tag, current weight and fuel cost to the lever's hover text, so
    ///     the price is visible before committing to a pull rather than only in a refusal message.
    /// </summary>
    /// <remarks>
    ///     Vanilla GetLeverHoverText returns one of two strings: "$piece_incinerator\n$piece_noaccess"
    ///     when PrivateArea.CheckAccess fails, otherwise
    ///     "[&lt;color=yellow&gt;&lt;b&gt;$KEY_Use&lt;/b&gt;&lt;/color&gt;] $piece_pulllever".
    ///     An untagged Obliterator is left byte-identical to vanilla.
    ///
    ///     Hover text is rebuilt every frame while the player looks at the lever, and resolving the
    ///     tag costs a Physics.OverlapSphere plus a walk of the inventory, so results are cached
    ///     per piece for a fraction of a second.
    /// </remarks>
    [HarmonyPatch(typeof(Incinerator), nameof(Incinerator.GetLeverHoverText))]
    internal static class LeverHoverPatch
    {
        private const float CacheSeconds = 0.5f;

        private sealed class Entry
        {
            internal string Suffix;
            internal float Expires;
        }

        private static readonly Dictionary<ZDOID, Entry> Cache = new Dictionary<ZDOID, Entry>();

        private static void Postfix(Incinerator __instance, ref string __result)
        {
            if (!QOConfig.Enabled.Value) return;

            // The no-access string already tells the player they cannot use it; adding a price is noise.
            if (string.IsNullOrEmpty(__result) || __result.Contains("$piece_noaccess")) return;

            var nview = __instance.m_nview;
            if (nview == null || !nview.IsValid()) return;

            var suffix = SuffixFor(__instance, nview.GetZDO().m_uid);
            if (!string.IsNullOrEmpty(suffix)) __result += "\n" + suffix;
        }

        private static string SuffixFor(Incinerator inc, ZDOID id)
        {
            var now = Time.realtimeSinceStartup;

            if (Cache.TryGetValue(id, out var entry) && now < entry.Expires) return entry.Suffix;

            var suffix = Build(inc);
            Cache[id] = new Entry { Suffix = suffix, Expires = now + CacheSeconds };
            return suffix;
        }

        private static string Build(Incinerator inc)
        {
            var tag = SignTags.ResolveLocal(inc.transform.position);
            if (tag.Length == 0) return string.Empty; // untagged: vanilla text, untouched

            var container = inc.m_container;
            var inv = container != null ? container.GetInventory() : null;
            if (inv == null) return string.Empty;

            var loc = Localization.instance;
            var line = Localize(loc, QOTokens.HoverShipTo).Replace("{tag}", tag);

            var weight = InventoryUtil.TotalWeight(inv);
            line += "  ·  " + Mathf.RoundToInt(weight);

            if (!QOConfig.RequireFuel.Value) return line;

            var quote = InventoryUtil.QuoteFuel(inv);

            var units = quote.Satisfied ? quote.UnitsNeeded : quote.ShortfallUnits;
            var fuelName = quote.Satisfied ? quote.SharedName : quote.ShortfallName;
            if (string.IsNullOrEmpty(fuelName)) return line;

            var cost = units + " " + Localize(loc, fuelName);

            // Colour the cost red when the Obliterator cannot currently pay it.
            if (!quote.Satisfied) cost = "<color=#ff6d6d>" + cost + "</color>";

            return line + "  ·  " + cost;
        }

        private static string Localize(Localization loc, string token)
        {
            return loc != null ? loc.Localize(token) : token;
        }
    }
}
