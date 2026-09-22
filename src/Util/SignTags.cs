using System.Collections.Generic;
using UnityEngine;

namespace QuantumObliterator.Util
{
    /// <summary>
    ///     Resolves the "tag" of an Obliterator: the text of the nearest sign within
    ///     <see cref="QOConfig.SignSearchRadius"/>.
    /// </summary>
    /// <remarks>
    ///     Text is always read from the sign's ZDO, never via <c>Sign.GetText()</c>.
    ///     <c>GetText()</c> returns the rendered TextMeshPro widget, which is profanity-filtered,
    ///     is the literal placeholder runes "..." when UGC view permission is denied, and is empty
    ///     for a frame before the sign's InvokeRepeating("UpdateText") first ticks. The ZDO value
    ///     is filter-free, permission-free and immediate.
    /// </remarks>
    internal static class SignTags
    {
        /// <summary>Prefabs that carry a Sign component. Verified exhaustively against the asset manifest.</summary>
        internal static readonly string[] SignPrefabs = { "sign", "sign_notext" };

        internal static string Normalise(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            // Signs are multi-line; collapse to a single logical tag so a wrapped sign still matches.
            return raw.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        internal static bool Matches(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            return string.Equals(a, b, QOConfig.TagComparison());
        }

        /// <summary>Read a sign ZDO's text. Returns empty for a blank or missing sign.</summary>
        internal static string TextOf(ZDO signZdo)
        {
            if (signZdo == null || !signZdo.IsValid()) return string.Empty;
            return Normalise(signZdo.GetString(ZDOVars.s_text, string.Empty));
        }

        /// <summary>
        ///     Server-side tag lookup for a possibly-unloaded Obliterator, given a pre-fetched
        ///     list of every sign ZDO in the world.
        /// </summary>
        internal static string ResolveFromZdos(ZDO obliterator, List<ZDO> allSigns)
        {
            if (obliterator == null || !obliterator.IsValid()) return string.Empty;

            var origin = obliterator.GetPosition();
            var radius = QOConfig.SignSearchRadius.Value;
            var bestSq = radius * radius;
            string best = string.Empty;

            for (int i = 0; i < allSigns.Count; i++)
            {
                var sign = allSigns[i];
                if (sign == null || !sign.IsValid()) continue;

                var d = (sign.GetPosition() - origin).sqrMagnitude;
                if (d > bestSq) continue;

                var text = TextOf(sign);
                if (text.Length == 0) continue; // a blank sign never claims a tag

                bestSq = d;
                best = text;
            }

            return best;
        }

        /// <summary>
        ///     Client-side tag lookup against loaded scene objects. Used only to decide whether a
        ///     lever pull is a transfer or a vanilla obliteration; the server re-resolves
        ///     authoritatively, so a stale answer here costs at most one rejected request.
        /// </summary>
        internal static string ResolveLocal(Vector3 origin)
        {
            var radius = QOConfig.SignSearchRadius.Value;
            var hits = Physics.OverlapSphere(origin, radius);
            if (hits == null || hits.Length == 0) return string.Empty;

            var bestSq = float.MaxValue;
            string best = string.Empty;

            foreach (var col in hits)
            {
                if (col == null) continue;

                var sign = col.GetComponentInParent<Sign>();
                if (sign == null) continue;

                var nview = sign.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid()) continue;

                var text = TextOf(nview.GetZDO());
                if (text.Length == 0) continue;

                var d = (sign.transform.position - origin).sqrMagnitude;
                if (d >= bestSq) continue;

                bestSq = d;
                best = text;
            }

            return best;
        }
    }
}
