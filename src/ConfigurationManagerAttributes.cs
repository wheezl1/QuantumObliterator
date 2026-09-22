// Standard ConfigurationManager attribute template.
//
// This class MUST live in the plugin's own assembly, in the global namespace.
// Jotunn and BepInEx.ConfigurationManager both locate it reflectively by name, so a
// reference to somebody else's copy does not work. Setting IsAdminOnly = true is what
// makes Jotunn's SynchronizationManager push the server's value to every client and lock
// the entry for non-admins.
//
// Ref: Jotunn.xml -> P:ConfigurationManagerAttributes.IsAdminOnly
//      "Whether a config is only writable by admins and gets overwritten on connecting clients"

using System;

// ReSharper disable All
#pragma warning disable 169, 414, 649

/// <summary>
///     Specifies how a setting is displayed inside the ConfigurationManager settings window.
/// </summary>
public sealed class ConfigurationManagerAttributes
{
    /// <summary>
    ///     Only admins may change this setting; connecting clients have it overwritten by
    ///     the server's value. This is the flag Jotunn keys config synchronisation off.
    /// </summary>
    public bool? IsAdminOnly;

    public bool? ShowRangeAsPercent;
    public Action<BepInEx.Configuration.ConfigEntryBase> CustomDrawer;
    public bool? Browsable;
    public string Category;
    public object DefaultValue;
    public bool? HideDefaultButton;
    public bool? HideSettingName;
    public string Description;
    public string DispName;
    public int? Order;
    public bool? ReadOnly;
    public bool? IsAdvanced;
    public Func<object, string> ObjToStr;
    public Func<string, object> StrToObj;
}
