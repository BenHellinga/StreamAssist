using System;
using StreamAssist.Core;

namespace StreamAssist.Features.Keyboard;

/// <summary>
/// One keyboard "slot" — a KeyboardMenu (config/device picker) paired with
/// the KeyboardDisplay it drives. Multiple slots can exist side by side.
/// Device selection is local to the slot; the config it points at (and that
/// config's key bindings) is shared globally with any other slot on the
/// same config name.
/// </summary>
public class KeyboardSlotViewModel : ViewModelBase, IDisposable
{
    public KeyboardMenuViewModel KeyboardMenu { get; }
    public KeyboardDisplayViewModel KeyboardDisplay { get; }

    public KeyboardSlotViewModel(InputManager inputManager, KeyboardFeatureSettings settings, KeyboardSlotData? initialData = null)
    {
        KeyboardMenu = new KeyboardMenuViewModel(settings);
        KeyboardDisplay = new KeyboardDisplayViewModel(inputManager, settings);

        if (initialData is not null)
            KeyboardMenu.InitializeFrom(initialData);

        // Push the (possibly restored) initial picks into the display —
        // KeyboardMenu's own change events are suppressed during init, so
        // this has to happen explicitly here.
        KeyboardDisplay.SelectedDeviceId = KeyboardMenu.SelectedDevice?.DevicePath;
        KeyboardDisplay.ApplyConfig(KeyboardMenu.SelectedConfigName ?? "Default");

        KeyboardMenu.DeviceChanged += OnDeviceChanged;
        KeyboardMenu.ConfigChanged += OnConfigChanged;
    }

    private void OnDeviceChanged(string? devicePath) => KeyboardDisplay.SelectedDeviceId = devicePath;
    private void OnConfigChanged(string configName) => KeyboardDisplay.ApplyConfig(configName);

    /// <summary>Snapshot of this slot's current picks, for persistence.</summary>
    public KeyboardSlotData CaptureState() => new()
    {
        SelectedDeviceId = KeyboardMenu.SelectedDevice?.DevicePath,
        SelectedConfigName = KeyboardMenu.SelectedConfigName ?? "Default"
    };

    public void Dispose()
    {
        KeyboardMenu.DeviceChanged -= OnDeviceChanged;
        KeyboardMenu.ConfigChanged -= OnConfigChanged;
        KeyboardMenu.Dispose();
        KeyboardDisplay.Dispose();
    }
}