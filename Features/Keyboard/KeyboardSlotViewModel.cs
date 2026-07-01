using System;
using StreamAssist.Core;

namespace StreamAssist.Features.Keyboard;



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

        KeyboardDisplay.SelectedDeviceId = KeyboardMenu.SelectedDevice?.DevicePath;
        KeyboardDisplay.ApplyConfig(KeyboardMenu.SelectedConfigName ?? "Default");

        KeyboardMenu.DeviceChanged += OnDeviceChanged;
        KeyboardMenu.ConfigChanged += OnConfigChanged;
    }



    private void OnDeviceChanged(string? devicePath) => KeyboardDisplay.SelectedDeviceId = devicePath;



    private void OnConfigChanged(string configName) => KeyboardDisplay.ApplyConfig(configName);



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