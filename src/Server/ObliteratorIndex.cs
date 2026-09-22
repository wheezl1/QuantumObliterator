using System.Collections.Generic;
using UnityEngine;

namespace QuantumObliterator.Server
{
    /// <summary>
    ///     World-wide enumeration of ZDOs by prefab. Server-side only -- only the server holds
    ///     every sector, so this is the one place that can see an Obliterator nobody is standing near.
    /// </summary>
    internal static class ObliteratorIndex
    {
        internal const string IncineratorPrefab = "incinerator";

        /// <summary>Hard ceiling on iteration batches, so a vanilla change can never hang the server.</summary>
        private const int MaxBatches = 100000;

        /// <summary>
        ///     How long a world scan stays warm. Each scan walks the whole sector array, and a
        ///     single transfer needs three of them (incinerators + two sign prefabs), so caching
        ///     matters. Kept short so that moving a sign takes effect almost immediately.
        /// </summary>
        private const float CacheSeconds = 5f;

        private sealed class Entry
        {
            internal List<ZDO> Zdos;
            internal float Expires;
        }

        private static readonly Dictionary<string, Entry> Cache = new Dictionary<string, Entry>();

        /// <summary>Drop every cached scan. Called when a transfer commits, so the next pull re-reads.</summary>
        internal static void Invalidate()
        {
            Cache.Clear();
        }

        /// <summary>
        ///     Cached <see cref="AllWithPrefab"/>. Returned ZDOs are re-filtered for validity on
        ///     every call, since a cached ZDO can be destroyed while the entry is still warm.
        /// </summary>
        internal static List<ZDO> AllWithPrefabCached(string prefab)
        {
            var now = Time.realtimeSinceStartup;

            if (!Cache.TryGetValue(prefab, out var entry) || now >= entry.Expires)
            {
                entry = new Entry { Zdos = AllWithPrefab(prefab), Expires = now + CacheSeconds };
                Cache[prefab] = entry;
                return new List<ZDO>(entry.Zdos);
            }

            var live = new List<ZDO>(entry.Zdos.Count);
            foreach (var zdo in entry.Zdos)
            {
                if (zdo != null && zdo.IsValid()) live.Add(zdo);
            }
            return live;
        }

        /// <summary>
        ///     Collect every ZDO in the world with the given prefab.
        /// </summary>
        /// <remarks>
        ///     ZDOMan.GetAllZDOsWithPrefab(string, List&lt;ZDO&gt;) does NOT exist on this build; only
        ///     the iterative form does. Decompiled, it is:
        ///
        ///         if (index >= m_objectsBySector.Length) {
        ///             ...scan m_objectsBySector[0] and every m_portalObjects bucket...
        ///             zdos.RemoveAll(InvalidZDO);
        ///             return true;                        // DONE
        ///         }
        ///         int n = 0;
        ///         while (index < m_objectsBySector.Length) {
        ///             var bucket = m_objectsBySector[index];
        ///             if (bucket != null) { ...scan...; if (++n > 400) return false; }
        ///             index++;
        ///         }
        ///         return false;                           // not done; final pass still pending
        ///
        ///     So true means finished, and the loop must run to completion -- the RemoveAll that
        ///     strips invalid ZDOs happens only on that final pass.
        ///
        ///     Note the final pass rescans m_objectsBySector[0], which the batch loop already
        ///     covered when index was 0 (verified: both sites reference field token 04001134).
        ///     Anything in sector 0 therefore comes back TWICE, and portal objects can be
        ///     double-counted the same way. Deduplication below is load-bearing, not defensive:
        ///     without it an Obliterator in sector 0 would be seen as its own duplicate and
        ///     rejected as an ambiguous tag.
        /// </remarks>
        internal static List<ZDO> AllWithPrefab(string prefab)
        {
            var result = new List<ZDO>();

            var zdoMan = ZDOMan.instance;
            if (zdoMan == null) return result;

            var raw = new List<ZDO>();
            int index = 0;
            int batches = 0;

            while (!zdoMan.GetAllZDOsWithPrefabIterative(prefab, raw, ref index))
            {
                if (++batches > MaxBatches)
                {
                    QOLog.Error($"GetAllZDOsWithPrefabIterative('{prefab}') did not terminate after " +
                                $"{MaxBatches} batches; aborting the scan. Vanilla behaviour may have changed.");
                    break;
                }
            }

            // Dedupe by ZDOID -- see the remark above.
            var seen = new HashSet<ZDOID>();
            foreach (var zdo in raw)
            {
                if (zdo == null || !zdo.IsValid()) continue;
                if (!seen.Add(zdo.m_uid)) continue;
                result.Add(zdo);
            }

            QOLog.Debug($"Scanned world for '{prefab}': {raw.Count} raw hit(s), {result.Count} unique, " +
                        $"{batches} batch call(s).");

            return result;
        }
    }
}
