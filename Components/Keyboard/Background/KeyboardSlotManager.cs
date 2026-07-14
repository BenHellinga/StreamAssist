using System;
using System.Collections.Generic;
using StreamAssist.Components.AppAction.Model;
using StreamAssist.Components.Keyboard.Model;
using StreamAssist.Components.Main;

namespace StreamAssist.Components.Keyboard.Background;



public class KeyboardSlotManager : IDisposable
{
    private readonly KeyboardInputManager _inputManager;
    private readonly KeyboardSlotModel _slotModel;

    private string? _device;
    private IReadOnlyDictionary<ushort, KeyboardBindingModel>? _keybinds;



    // constructor



    // TODO the current method of checking devices/keys could be improved with centralized dictionaries, for example Dictionary<device, [Dictionary<key, KeyboardBindingModel>]>
    // it would not be recommended to share a dictionary accross an entire config, as stateful actions could trigger multiple times with two slots on the same config and device, so they need different instances, hence different dictionaries
    public KeyboardSlotManager(KeyboardInputManager inputManager, KeyboardListModel listModel, KeyboardSlotModel slotModel)
    {
        _inputManager = inputManager;
        _slotModel = slotModel;

        _device = _slotModel.SelectedDevice?.DevicePath;
        _keybinds = _slotModel.SelectedConfig?.Keybinds;

        _slotModel.SelectedDeviceChanged += OnSelectedDeviceChanged;
        _slotModel.SelectedConfigChanged += OnSelectedConfigChanged;

        _inputManager.KeyPressed += OnKeyPressed;
        _inputManager.KeyReleased += OnKeyReleased;
        _inputManager.KeyRepeated += OnKeyRepeated;
    }



    // model callbacks



    private void OnSelectedDeviceChanged(KeyboardDeviceModel? _, KeyboardDeviceModel? newDevice) => _device = newDevice?.DevicePath;



    private void OnSelectedConfigChanged(KeyboardConfigModel? _, KeyboardConfigModel? newConfig) => _keybinds = newConfig?.Keybinds;



    // key events



    private void OnKeyPressed(string device, ushort key)
    {
        if (device != _device) return;
        if (_keybinds == null) return;
        if (!_keybinds.TryGetValue(key, out var bindingModel)) return;

        bindingModel.OnPressed.SafeExecuteAllAsync();
    }



    private void OnKeyReleased(string device, ushort key)
    {
        if (device != _device) return;
        if (_keybinds == null) return;
        if (!_keybinds.TryGetValue(key, out var bindingModel)) return;

        bindingModel.OnReleased.SafeExecuteAllAsync();
    }



    private void OnKeyRepeated(string device, ushort key)
    {
        if (device != _device) return;
        if (_keybinds == null) return;
        if (!_keybinds.TryGetValue(key, out var bindingModel)) return;

        bindingModel.OnRepeated.SafeExecuteAllAsync();
    }



    // disposal



    public void Dispose()
    {
        _slotModel.SelectedDeviceChanged -= OnSelectedDeviceChanged;
        _slotModel.SelectedConfigChanged -= OnSelectedConfigChanged;

        _inputManager.KeyPressed -= OnKeyPressed;
        _inputManager.KeyReleased -= OnKeyReleased;
        _inputManager.KeyRepeated -= OnKeyRepeated;
    }
}