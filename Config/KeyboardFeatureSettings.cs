using StreamAssist.Config;

namespace StreamAssist.Features.Keyboard;

/// <summary>
/// The settings stored per named config inside keyboard.json.
/// Add more keyboard-related settings here as the feature grows.
/// </summary>
public class KeyboardConfig
{
    /// <summary>
    /// Null means "listen to all devices".
    /// Set to a DevicePath string (e.g. "\\?\HID#VID_045E&amp;PID_0800#...") to filter to one device.
    /// </summary>
    public string? SelectedDeviceId { get; set; }
}

/// <summary>
/// Manages keyboard.json — multiple named configs, one active at a time.
///
/// On disk (%AppData%/StreamAssist/keyboard.json):
/// {
///   "activeConfig": "Default",
///   "configs": {
///     "Default":      { "selectedDeviceId": null },
///     "StreamingDay": { "selectedDeviceId": "VID_045E&amp;PID_0800" }
///   }
/// }
/// </summary>
public class KeyboardFeatureSettings : FeatureSettings<KeyboardConfig>
{
    public KeyboardFeatureSettings() : base("keyboard") { }
}
