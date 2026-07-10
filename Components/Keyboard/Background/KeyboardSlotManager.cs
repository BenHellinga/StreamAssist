using System;
using System.Collections.Generic;
using StreamAssist.Components.StreamAction;
using StreamAssist.Components.Keyboard.Model;
using StreamAssist.Components.Main;

namespace StreamAssist.Components.Keyboard.Background;



public class KeyboardSlotManager : IDisposable
{
    private readonly KeyboardInputManager _inputManager;
    private readonly KeyboardListModel _listModel; // TODO should be removed when actions and configs are finished
    private readonly KeyboardSlotModel _slotModel;

    private string? _device;
    private Dictionary<ushort, IStreamAction> _keybinds;



    // constructor



    public KeyboardSlotManager(KeyboardInputManager inputManager, KeyboardListModel listModel, KeyboardSlotModel slotModel)
    {
        _inputManager = inputManager;
        _listModel = listModel;
        _slotModel = slotModel;

        _device = _slotModel.SelectedDevice?.DevicePath;
        _keybinds = _listModel.GetConfigBindings(_slotModel.SelectedConfig?.Name ?? "");

        _slotModel.SelectedDeviceChanged += OnSelectedDeviceChanged;
        _slotModel.SelectedConfigChanged += OnSelectedConfigChanged;
        _listModel.ConfigBindingChanged += OnConfigBindingChanged;

        _inputManager.KeyPressed += OnKeyPressed;
        _inputManager.KeyReleased += OnKeyReleased;
    }



    // model callbacks



    private void OnSelectedDeviceChanged(KeyboardDeviceModel? device) => _device = device?.DevicePath;



    private void OnSelectedConfigChanged(KeyboardConfigModel? configName) => _keybinds = _listModel.GetConfigBindings(configName?.Name ?? "");



    private void OnConfigBindingChanged(string configName)
    {
        if (configName != _slotModel.SelectedConfig?.Name) return;
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
        _slotModel.SelectedConfigChanged -= OnSelectedConfigChanged;
        _listModel.ConfigBindingChanged -= OnConfigBindingChanged;

        _inputManager.KeyPressed -= OnKeyPressed;
        _inputManager.KeyReleased -= OnKeyReleased;
    }
}