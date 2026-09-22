using System.Collections.Generic;
using QuantumObliterator.Util;

namespace QuantumObliterator.Server
{
    /// <summary>
    ///     Server-authoritative transfer. Resolves the tag pair, validates, and performs BOTH
    ///     halves of the move by writing the two container ZDOs directly.
    /// </summary>
    /// <remarks>
    ///     This runs only where every sector is in memory -- a dedicated server, or the host of a
    ///     client-hosted world. ZDO.Set performs no ownership check: it bumps DataRevision and
    ///     marks the sector dirty, and the normal per-peer sync list carries the change out. A
    ///     loaded destination container picks it up by itself, because Container.CheckForChanges
    ///     compares ZDO.DataRevision against its cached m_lastRevision and calls Load().
    /// </remarks>
    internal static class TransferService
    {
        internal static TransferOutcome Execute(ZDOID sourceId, long requesterPeerId, int expectedStacks)
        {
            if (!QOConfig.Enabled.Value) return TransferOutcome.Fail(QOResult.Disabled);

            var znet = ZNet.instance;
            if (znet == null || !znet.IsServer())
            {
                QOLog.Warn("TransferService.Execute called on a non-server instance; refusing.");
                return TransferOutcome.Fail(QOResult.NotServer);
            }

            var zdoMan = ZDOMan.instance;
            if (zdoMan == null) return TransferOutcome.Fail(QOResult.Error);

            var srcZdo = zdoMan.GetZDO(sourceId);
            if (srcZdo == null || !srcZdo.IsValid())
            {
                QOLog.Warn($"Transfer source {sourceId} is not a live ZDO.");
                return TransferOutcome.Fail(QOResult.Error);
            }

            // --- resolve the source tag -------------------------------------------------
            var signs = new List<ZDO>();
            foreach (var signPrefab in SignTags.SignPrefabs)
            {
                signs.AddRange(ObliteratorIndex.AllWithPrefabCached(signPrefab));
            }

            var tag = SignTags.ResolveFromZdos(srcZdo, signs);
            if (tag.Length == 0)
            {
                QOLog.Debug($"Source {sourceId} has no sign within {QOConfig.SignSearchRadius.Value}m.");
                return TransferOutcome.Fail(QOResult.Error);
            }

            // --- find the partner -------------------------------------------------------
            var candidates = new List<ZDO>();
            foreach (var zdo in ObliteratorIndex.AllWithPrefabCached(ObliteratorIndex.IncineratorPrefab))
            {
                if (zdo == null || !zdo.IsValid()) continue;
                if (zdo.m_uid == sourceId) continue;
                if (SignTags.Matches(SignTags.ResolveFromZdos(zdo, signs), tag)) candidates.Add(zdo);
            }

            if (candidates.Count == 0)
            {
                QOLog.Debug($"No partner Obliterator tagged '{tag}'.");
                return TransferOutcome.Fail(QOResult.NoPartner, tag);
            }

            if (candidates.Count > 1)
            {
                // A tag names exactly one pair. Refusing keeps the destination unambiguous, and
                // means a stranger reusing your tag breaks the link rather than stealing cargo.
                QOLog.Debug($"{candidates.Count} Obliterators share the tag '{tag}'; refusing.");
                return TransferOutcome.Fail(QOResult.Ambiguous, tag, candidates.Count);
            }

            var dstZdo = candidates[0];

            // --- load both inventories as detached copies -------------------------------
            if (!InventoryUtil.TryGetGrid(srcZdo, out var sw, out var sh, out var sname) ||
                !InventoryUtil.TryGetGrid(dstZdo, out var dw, out var dh, out var dname))
            {
                QOLog.Error("Could not read Obliterator container dimensions from ZNetScene.");
                return TransferOutcome.Fail(QOResult.Error);
            }

            var srcInv = InventoryUtil.Read(srcZdo, sw, sh, sname);
            var dstInv = InventoryUtil.Read(dstZdo, dw, dh, dname);

            // The server transfers ITS copy of the source, which is whatever the owning client
            // last synced. If that disagrees with what the client saw when it pulled the lever,
            // committing would write an empty inventory over items the server never saw. Bail
            // instead: failing safe and asking for a retry beats silently eating cargo.
            if (expectedStacks >= 0 && srcInv.NrOfItems() != expectedStacks)
            {
                QOLog.Warn($"Source {sourceId} desynced: client saw {expectedStacks} stack(s), " +
                           $"server sees {srcInv.NrOfItems()}. Aborting.");
                return TransferOutcome.Fail(QOResult.Desync);
            }

            // --- charge the transfer ----------------------------------------------------
            // Priced on the weight of everything currently inside, so the quote must be taken
            // BEFORE any fuel is removed.
            var quote = InventoryUtil.QuoteFuel(srcInv);

            if (!quote.Satisfied)
            {
                QOLog.Debug($"Source {sourceId} cannot pay: needs {quote.ShortfallUnits} " +
                            $"x {quote.ShortfallName ?? "<none configured>"}.");
                return TransferOutcome.Fail(QOResult.NoFuel, quote.ShortfallName ?? "", quote.ShortfallUnits);
            }

            if (quote.UnitsNeeded > 0)
            {
                srcInv.RemoveItem(quote.SharedName, quote.UnitsNeeded, -1, true);

                // Burning the fuel emptied the Obliterator, so the pull would cost a Thunderstone
                // and deliver nothing. Refuse instead, having written nothing.
                if (srcInv.NrOfItems() == 0)
                {
                    QOLog.Debug($"Source {sourceId} holds nothing but fuel; refusing.");
                    return TransferOutcome.Fail(QOResult.NothingToShip);
                }
            }

            // --- move everything, or nothing --------------------------------------------
            if (!InventoryUtil.MoveAllOrNothing(srcInv, dstInv, out var movedStacks))
            {
                QOLog.Debug($"Destination '{tag}' could not hold the whole shipment; aborting.");
                return TransferOutcome.Fail(QOResult.DestinationFull, tag);
            }

            // --- commit: both blobs are built before either ZDO is touched --------------
            var srcBytes = InventoryUtil.Serialise(srcInv);
            var dstBytes = InventoryUtil.Serialise(dstInv);

            dstZdo.Set(ZDOVars.s_items, dstBytes);
            srcZdo.Set(ZDOVars.s_items, srcBytes);

            // Nudge the sync along rather than waiting for the next dirty-sector pass.
            if (requesterPeerId != 0L) zdoMan.ForceSendZDO(requesterPeerId, sourceId);
            zdoMan.ForceSendZDO(dstZdo.m_uid);

            // Both containers just changed; force the next pull to re-read the world.
            ObliteratorIndex.Invalidate();

            QOLog.Info($"Transferred {movedStacks} stack(s) via tag '{tag}': {sourceId} -> {dstZdo.m_uid}.");

            return new TransferOutcome
            {
                Result = QOResult.Success,
                Detail = tag,
                Amount = movedStacks,
                Destination = dstZdo.m_uid,
            };
        }
    }
}
