using System;
using System.Collections.Generic;
using StreamAssist.Components.Keyboard.Model;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Keyboard.Background;



// turns key presses into bindable triggers by looking them up in the model each time
// (nothing is cached, so config/device/bind changes take effect immediately with no syncing)
public class KeybindManager : IDisposable
{
    // private



    private readonly KeyboardInputManager _inputManager;
    private readonly KeyboardListModel _listModel;



    // constructor



    public KeybindManager(KeyboardListModel listModel, KeyboardInputManager inputManager)
    {
        _inputManager = inputManager;
        _listModel = listModel;

        _inputManager.KeyDown += OnKeyDown;
        _inputManager.KeyUp += OnKeyUp;
    }



    // key events



    private void OnKeyDown(string device, ushort vk)
    {
        foreach (var bindable in GetBindings(device, vk))
            bindable.TriggerStart();
    }



    private void OnKeyUp(string device, ushort vk)
    {
        foreach (var bindable in GetBindings(device, vk))
            bindable.TriggerStop();
    }



    // helpers



    // every slot using this keyboard, and the bindable its selected config has on this key
    // (returned as a list first, since triggering can change slots/configs while we loop)
    private List<BindableModel> GetBindings(string device, ushort vk)
    {
        var bindings = new List<BindableModel>();

        foreach (var slot in _listModel.Slots)
        {
            if (slot.SelectedDevice?.DevicePath != device) continue;
            if (slot.SelectedConfig is not { } config) continue;

            if (config.Keybinds.TryGetValue(vk, out var bindable))
                bindings.Add(bindable);
        }

        return bindings;
    }



    // disposal



    public void Dispose()
    {
        _inputManager.KeyDown -= OnKeyDown;
        _inputManager.KeyUp -= OnKeyUp;
    }
}
