using System;
using System.Collections.Generic;
using StreamAssist.Components.StreamAction;
using StreamAssist.Components.Keyboard.Model;
using StreamAssist.Components.Main;

namespace StreamAssist.Components.Keyboard.Background;



public class KeyboardSlotManager : IDisposable
{
    private readonly KeyboardInputManager _inputManager;
    private readonly KeyboardListModel _listModel;
    private readonly KeyboardSlotModel _slotModel;

    private string _device;
    private Dictionary<ushort, IStreamAction> _keybinds;



    // constructor



    public KeyboardSlotManager(KeyboardInputManager inputManager, KeyboardListModel listModel, KeyboardSlotModel slotModel)
    {
        _inputManager = inputManager;
        _listModel = listModel;
        _slotModel = slotModel;

        _device = _slotModel.SelectedDevice;
        _keybinds = _listModel.GetConfigBindings(_slotModel.SelectedConfig);

        _slotModel.SelectedDeviceChanged += OnSelectedDeviceChanged;
        _slotModel.ConfigChanged += OnConfigChanged;
        _listModel.ConfigBindingsChanged += OnConfigsChanged;

        _inputManager.KeyPressed += OnKeyPressed;
        _inputManager.KeyReleased += OnKeyReleased;
    }



    // model callbacks



    private void OnSelectedDeviceChanged(string device) => _device = device;

    private void OnConfigChanged(string configName) => _keybinds = _listModel.GetConfigBindings(configName);

    private void OnConfigsChanged(string configName)
    {
        if (configName != _slotModel.SelectedConfig) return;
        _keybinds = _listModel.GetConfigBindings(configName);
    }


    // key events



    private void OnKeyPressed(string device, ushort vk)
    {
        if (device != _device) return;
        if (!_keybinds.TryGetValue(vk, out var action)) return;

        _ = action.SafeExecuteAsync();
    }



    private void OnKeyReleased(string device, ushort vk)
    {
        if (device != _device) return;
        if (!_keybinds.TryGetValue(vk, out var action)) return;

        // action needs onrelease trigger
    }



    // disposal



    public void Dispose()
    {
        _slotModel.SelectedDeviceChanged -= OnSelectedDeviceChanged;
        _slotModel.ConfigChanged -= OnConfigChanged;
        _listModel.ConfigBindingsChanged -= OnConfigsChanged;

        _inputManager.KeyPressed -= OnKeyPressed;
        _inputManager.KeyReleased -= OnKeyReleased;
    }
}