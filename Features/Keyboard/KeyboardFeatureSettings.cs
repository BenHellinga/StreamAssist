using System.Collections.Generic;
using System.Text.Json.Serialization;
using StreamAssist.Config;
using System;

namespace StreamAssist.Features.Keyboard;

/// <summary>
/// The settings stored per named config inside keyboard.json. A config is now
/// purely a set of key bindings — it's shared by name across every keyboard
/// slot that selects it (two slots both on "Streaming" share the same bindings).
/// Device selection lives per-slot instead (see KeyboardSlotData).
/// </summary>
public class KeyboardConfig
{
    /// <summary>VK code → bound action's Type.FullName. Resolved back to an
    /// IFeatureAction instance via ActionResolver. Keys with no entry here
    /// are unbound.</summary>
    [JsonPropertyName("keyBindings")]
    public Dictionary<ushort, string> KeyBindings { get; set; } = new();
}

/// <summary>One keyboard slot's persisted picks: which physical device it's
/// listening to, and which (shared, named) config drives its key bindings.</summary>
public class KeyboardSlotData
{
    [JsonPropertyName("selectedDeviceId")]
    public string? SelectedDeviceId { get; set; }

    [JsonPropertyName("selectedConfigName")]
    public string SelectedConfigName { get; set; } = "Default";
}

/// <summary>Root-level keyboard data: the full list of slots, in order. The
/// list's length is how many keyboard displays are shown — no separate count needed.</summary>
public class KeyboardRootData
{
    [JsonPropertyName("slots")]
    public List<KeyboardSlotData> Slots { get; set; } = new() { new KeyboardSlotData() };
}

/// <summary>
/// Manages keyboard.json — globally-shared named configs (key bindings), plus
/// per-slot device/config selections and slot count.
///
/// On disk (local Config dir, see FeatureSettings.RootDir):
/// {
///   "activeConfig": "Default",
///   "configs": {
///     "Default":      { "keyBindings": {} },
///     "Streaming":     { "keyBindings": { "65": "StreamAssist.Features.SomeAction" } }
///   },
///   "rootData": {
///     "slots": [
///       { "selectedDeviceId": null, "selectedConfigName": "Default" },
///       { "selectedDeviceId": "\\?\\HID#...", "selectedConfigName": "Streaming" }
///     ]
///   }
/// }
///
/// Note: "activeConfig"/Active/SwitchConfig are inherited from the base class
/// but unused here — keyboard slots each track their own selected config
/// independently via RootData.Slots rather than one global "active" one.
/// </summary>
public class KeyboardFeatureSettings : FeatureSettings<KeyboardConfig, KeyboardRootData>
{
    public KeyboardFeatureSettings() : base("keyboard") { }

    /// <summary>Raised after a key binding inside some config changes (added,
    /// changed, or removed) — lets every KeyboardDisplayViewModel currently
    /// showing that config refresh itself, not just the one that made the edit.</summary>
    public event Action<string>? BindingsChanged;

    /// <summary>Persist a key-binding edit and notify anyone watching that config.
    /// Use this instead of the inherited Save() when the change is to a
    /// config's KeyBindings specifically.</summary>
    public void SaveAndNotifyBindingsChanged(string configName)
    {
        Save();
        BindingsChanged?.Invoke(configName);
    }
}