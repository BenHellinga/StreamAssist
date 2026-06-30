using System;
using StreamAssist.Core;

namespace StreamAssist.Features.Keyboard;



/// <summary>
/// One keyboard "slot" — a KeyboardMenu (config/device picker) paired with
/// the KeyboardDisplay it drives. Multiple slots can exist side by side;
/// each is fully independent (own configs, own selected device, own key
/// bindings).
/// </summary>
public class KeyboardSlotViewModel : ViewModelBase, IDisposable
{
    public KeyboardMenuViewModel KeyboardMenu { get; }
    public KeyboardDisplayViewModel KeyboardDisplay { get; }



    public KeyboardSlotViewModel(InputManager inputManager)
    {
        KeyboardMenu = new KeyboardMenuViewModel();
        KeyboardDisplay = new KeyboardDisplayViewModel(inputManager)
        {
            SelectedDeviceId = KeyboardMenu.SelectedDevice?.DevicePath
        };

        KeyboardMenu.DeviceChanged += OnDeviceChanged;
    }



    private void OnDeviceChanged(string? devicePath) => KeyboardDisplay.SelectedDeviceId = devicePath;



    public void Dispose()
    {
        KeyboardMenu.DeviceChanged -= OnDeviceChanged;
        KeyboardDisplay.Dispose();
    }
}