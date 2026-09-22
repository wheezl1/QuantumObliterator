using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;
using QuantumObliterator.Net;

namespace QuantumObliterator
{
    /// <summary>
    ///     Turns the Obliterator into a paired, fuel-charged freight terminal: two Obliterators
    ///     whose nearby signs read the same thing become a link, and pulling the lever on one
    ///     ships its contents to the other, anywhere in the world.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    // The client initiates the request, so a vanilla client on a modded server would silently
    // fall through to plain obliteration and destroy its cargo. Everyone must have the mod.
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "wheezl.quantumobliterator";
        public const string PluginName = "QuantumObliterator";

        // Single source of truth: <PluginVersion> in QuantumObliterator.csproj, surfaced here
        // through the generated obj/PluginInfo.g.cs. It is a const, so the attribute is happy.
        public const string PluginVersion = PluginInfo.Version;

        private Harmony _harmony;

        private void Awake()
        {
            QOLog.Source = Logger;

            QOConfig.Bind(Config);
            AddLocalization();
            QORpc.Register();

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();

            Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        private static void AddLocalization()
        {
            var loc = LocalizationManager.Instance.GetLocalization();

            loc.AddTranslation("English", new Dictionary<string, string>
            {
                { "qo_msg_success",   "Shipment sent to \"{tag}\"" },
                { "qo_msg_nopartner", "No other Obliterator is tagged \"{tag}\"" },
                { "qo_msg_ambiguous", "More than one Obliterator is tagged \"{tag}\"" },
                { "qo_msg_nofuel",    "Requires {n} {item} to charge" },
                { "qo_msg_full",      "Destination Obliterator is full" },
                { "qo_msg_desync",    "Obliterator contents changed - try again" },
                { "qo_msg_nothingtoship", "Nothing to ship except the fuel" },
                { "qo_msg_timeout",   "No response from the server" },
                { "qo_msg_disabled",  "Obliterator linking is disabled" },
                { "qo_msg_error",     "Transfer failed" },
                { "qo_hover_shipto",  "Ship to \"{tag}\"" },
            });
        }
    }
}
