using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace QuantumObliterator
{
    /// <summary>
    ///     All tunables. Every gameplay-affecting entry is marked IsAdminOnly so Jotunn
    ///     synchronises the server's value down to connecting clients and locks it there.
    ///     The plugin's default config file is synced implicitly; only *extra* config files
    ///     would need SynchronizationManager.RegisterCustomConfig.
    /// </summary>
    internal static class QOConfig
    {
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> SignSearchRadius;
        internal static ConfigEntry<bool> CaseSensitiveTags;
        internal static ConfigEntry<bool> RequireFuel;
        internal static ConfigEntry<string> FuelItems;
        internal static ConfigEntry<float> WeightPerFuelUnit;
        internal static ConfigEntry<bool> DestinationEffect;
        internal static ConfigEntry<bool> VerboseLogging;

        /// <summary>Parsed, cached view of <see cref="FuelItems"/>.</summary>
        private static string[] _fuelCache;
        private static string _fuelCacheRaw;

        internal static void Bind(ConfigFile cfg)
        {
            Enabled = cfg.Bind("1 - General", "Enabled", true,
                new ConfigDescription(
                    "Master switch. When false every Obliterator behaves exactly like vanilla.",
                    null, Admin()));

            SignSearchRadius = cfg.Bind("2 - Tagging", "SignSearchRadius", 5.0f,
                new ConfigDescription(
                    "How far from an Obliterator to look for a sign, in metres. The nearest sign " +
                    "within this radius supplies the tag.",
                    new AcceptableValueRange<float>(1f, 32f), Admin()));

            CaseSensitiveTags = cfg.Bind("2 - Tagging", "CaseSensitiveTags", false,
                new ConfigDescription(
                    "When false, \"Ashlands\" and \"ashlands\" are the same tag. Tags are always " +
                    "trimmed of surrounding whitespace.",
                    null, Admin()));

            RequireFuel = cfg.Bind("3 - Fuel", "RequireFuel", true,
                new ConfigDescription(
                    "Whether transfers cost fuel at all. Set to false for free, unlimited shipping.",
                    null, Admin()));

            FuelItems = cfg.Bind("3 - Fuel", "FuelItems", "Thunderstone",
                new ConfigDescription(
                    "Comma-separated item PREFAB names that can charge a transfer. They are tried " +
                    "in order and the first one the Obliterator holds enough of is consumed. " +
                    "Exact capitalisation matters (e.g. 'Thunderstone', 'SurtlingCore').",
                    null, Admin()));

            WeightPerFuelUnit = cfg.Bind("3 - Fuel", "WeightPerFuelUnit", 1080f,
                new ConfigDescription(
                    "How much shipment weight a single unit of fuel pays for. 1080 is roughly three " +
                    "full stacks of metal bars. The consumed fuel's own weight is on top of this, " +
                    "so it never eats into the allowance.",
                    new AcceptableValueRange<float>(1f, 100000f), Admin()));

            DestinationEffect = cfg.Bind("4 - Effects", "DestinationEffect", true,
                new ConfigDescription(
                    "Play the lightning effect at the receiving Obliterator too, so players " +
                    "standing there see the delivery arrive.",
                    null, Admin()));

            // Local-only: a client's own log verbosity is nobody else's business.
            VerboseLogging = cfg.Bind("5 - Debug", "VerboseLogging", false,
                "Log detailed tag resolution and transfer decisions to LogOutput.log.");
        }

        private static ConfigurationManagerAttributes Admin()
        {
            return new ConfigurationManagerAttributes { IsAdminOnly = true };
        }

        /// <summary>Fuel prefab names, parsed and cached until the config string changes.</summary>
        internal static string[] FuelPrefabNames()
        {
            var raw = FuelItems.Value ?? string.Empty;
            if (raw != _fuelCacheRaw)
            {
                var list = new List<string>();
                foreach (var part in raw.Split(','))
                {
                    var t = part.Trim();
                    if (t.Length > 0) list.Add(t);
                }
                _fuelCache = list.ToArray();
                _fuelCacheRaw = raw;
            }
            return _fuelCache ?? Array.Empty<string>();
        }

        internal static StringComparison TagComparison()
        {
            return CaseSensitiveTags.Value
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;
        }
    }
}
