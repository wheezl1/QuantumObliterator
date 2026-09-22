using System.Collections.Generic;
using UnityEngine;

namespace QuantumObliterator.Util
{
    /// <summary>Reading and writing a Container's inventory straight through its ZDO.</summary>
    /// <remarks>
    ///     Mirrors vanilla Container.Save/Load exactly: the payload is a <c>byte[]</c> under
    ///     <c>ZDOVars.s_items</c> holding a serialised ZPackage. It is NOT a base64 string --
    ///     base64 s_items is the legacy format that ZDOMan.ConvertInventories migrates away from.
    /// </remarks>
    internal static class InventoryUtil
    {
        /// <summary>Grid dimensions of a container prefab, read from ZNetScene rather than hardcoded.</summary>
        internal static bool TryGetGrid(ZDO zdo, out int width, out int height, out string name)
        {
            width = height = 0;
            name = "container";

            var scene = ZNetScene.instance;
            if (scene == null) return false;

            var prefab = scene.GetPrefab(zdo.GetPrefab());
            if (prefab == null) return false;

            // GetComponentInChildren also covers a container mounted on a child object.
            var container = prefab.GetComponentInChildren<Container>(true);
            if (container == null) return false;

            width = container.m_width;
            height = container.m_height;
            name = string.IsNullOrEmpty(container.m_name) ? "container" : container.m_name;
            return width > 0 && height > 0;
        }

        /// <summary>Deserialise a container ZDO's items into a detached Inventory.</summary>
        internal static Inventory Read(ZDO zdo, int width, int height, string name)
        {
            var inv = new Inventory(name, null, width, height);

            var bytes = zdo.GetByteArray(ZDOVars.s_items, null);
            if (bytes != null && bytes.Length > 0)
            {
                inv.Load(new ZPackage(bytes));
            }

            return inv;
        }

        /// <summary>Serialise an Inventory back to the wire format Container.Load expects.</summary>
        internal static byte[] Serialise(Inventory inv)
        {
            var pkg = new ZPackage();
            inv.Save(pkg);
            return pkg.GetArray();
        }

        /// <summary>
        ///     Move every item from <paramref name="from"/> into <paramref name="to"/>.
        ///     Returns false if anything was left behind.
        /// </summary>
        /// <remarks>
        ///     Inventory.AddItem is a PARTIAL add: for stackables it feeds units into existing
        ///     stacks one at a time, mutating item.m_stack downward, and only returns false once
        ///     it runs out of empty slots -- by which point it may already have added some units.
        ///     MoveItemToThis removes from the source only when AddItem returned true.
        ///
        ///     That is safe here only because both inventories are detached copies deserialised
        ///     from ZDO blobs. A partial move dirties the copies and we simply discard them
        ///     without writing either ZDO, so the authoritative world state is untouched.
        ///     Never run this against a live Container inventory.
        /// </remarks>
        internal static bool MoveAllOrNothing(Inventory from, Inventory to, out int movedStacks)
        {
            movedStacks = 0;

            // Snapshot: MoveItemToThis mutates the source list while we iterate it.
            var items = new List<ItemDrop.ItemData>(from.GetAllItems());

            foreach (var item in items)
            {
                to.MoveItemToThis(from, item);
            }

            movedStacks = items.Count;

            // The single authoritative check: if the source still holds anything, it did not fit.
            if (from.NrOfItems() > 0)
            {
                movedStacks = 0;
                return false;
            }

            return true;
        }

        /// <summary>Total weight of an inventory, summed explicitly rather than read from the cache.</summary>
        /// <remarks>
        ///     <c>Inventory.GetTotalWeight()</c> returns the cached <c>m_totalWeight</c> field, which
        ///     <c>UpdateTotalWeight()</c> refreshes from <c>Changed()</c>. That is reliable for a freshly
        ///     loaded inventory, but we mutate these detached copies, so summing is cheaper to reason
        ///     about than tracking whether the cache is still warm.
        ///     <c>GetWeight(-1)</c> is the stack TOTAL and applies quality scaling.
        /// </remarks>
        internal static float TotalWeight(Inventory inv)
        {
            var total = 0f;
            foreach (var item in inv.GetAllItems())
            {
                if (item != null) total += item.GetWeight(-1);
            }
            return total;
        }

        /// <summary>What a transfer costs, and whether the Obliterator can pay it.</summary>
        internal struct FuelQuote
        {
            /// <summary>True when the cost can be paid (or no fuel is required at all).</summary>
            internal bool Satisfied;

            /// <summary>Shared item name of the fuel to consume, e.g. "$item_thunderstone".</summary>
            internal string SharedName;

            /// <summary>Units of <see cref="SharedName"/> to consume. Zero when fuel is disabled.</summary>
            internal int UnitsNeeded;

            /// <summary>Shared name to name in the shortfall message when nothing is affordable.</summary>
            internal string ShortfallName;

            /// <summary>How many units of <see cref="ShortfallName"/> the player would need.</summary>
            internal int ShortfallUnits;
        }

        /// <summary>
        ///     Price a shipment. Cost scales with the weight of everything in the Obliterator.
        /// </summary>
        /// <remarks>
        ///     With W = total inventory weight, C = the per-unit weight allowance and fw = the fuel
        ///     item's own unit weight:
        ///
        ///         k = max(1, ceil(W / (C + fw)))
        ///
        ///     Each unit of fuel pays for its own weight PLUS C, so the fuel that gets burned never
        ///     eats into the allowance: permitted cargo is k*C, and cargo + k*fw &lt;= k*(C + fw).
        ///     Spare fuel left behind is shipped as ordinary cargo and does count toward W.
        /// </remarks>
        internal static FuelQuote QuoteFuel(Inventory inv)
        {
            if (!QOConfig.RequireFuel.Value)
            {
                return new FuelQuote { Satisfied = true, UnitsNeeded = 0, SharedName = null };
            }

            var quote = new FuelQuote { Satisfied = false };

            var names = QOConfig.FuelPrefabNames();
            if (names.Length == 0)
            {
                QOLog.Warn("RequireFuel is on but FuelItems is empty; no transfer can be paid for.");
                return quote;
            }

            var odb = ObjectDB.instance;
            if (odb == null)
            {
                QOLog.Warn("ObjectDB unavailable; cannot resolve fuel items.");
                return quote;
            }

            var totalWeight = TotalWeight(inv);
            var allowance = QOConfig.WeightPerFuelUnit.Value;

            foreach (var prefabName in names)
            {
                var prefab = odb.GetItemPrefab(prefabName);
                if (prefab == null)
                {
                    QOLog.Warn($"Fuel item '{prefabName}' is not a known item prefab - check the FuelItems config.");
                    continue;
                }

                var drop = prefab.GetComponent<ItemDrop>();
                if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null) continue;

                var shared = drop.m_itemData.m_shared.m_name;

                // Prefer the weight of an actual stack in this inventory, so item quality is taken
                // into account; fall back to the prefab's own unit weight when none is present.
                var unitWeight = drop.m_itemData.GetNonStackedWeight();
                var held = inv.GetItem(shared, -1, false);
                if (held != null) unitWeight = held.GetNonStackedWeight();

                // Never divide by ~zero: a weightless fuel with a tiny allowance must not demand
                // an astronomical count.
                var perUnit = Mathf.Max(allowance + unitWeight, 0.01f);

                // Weight is an accumulated float sum, so an exactly-on-budget shipment can land a
                // hair above 1.0 and demand a second unit. Shave an epsilon before rounding up so
                // "exactly three stacks of bars" costs exactly one stone.
                var needed = Mathf.Max(1, Mathf.CeilToInt(totalWeight / perUnit - 0.0001f));

                if (quote.ShortfallName == null)
                {
                    quote.ShortfallName = shared;
                    quote.ShortfallUnits = needed;
                }

                if (inv.CountItems(shared, -1, true) >= needed)
                {
                    quote.Satisfied = true;
                    quote.SharedName = shared;
                    quote.UnitsNeeded = needed;
                    return quote;
                }
            }

            return quote;
        }
    }
}
