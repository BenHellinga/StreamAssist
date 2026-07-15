using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Core;
using StreamAssist.Components.Keyboard.Background;
using StreamAssist.Components.Keyboard.Model;
using StreamAssist.Components.Main.Model;
using StreamAssist.Components.AppAction.Model;
using System;
using Avalonia.Data;
using Avalonia.Controls;

namespace StreamAssist.Components.Keyboard.UI;



public partial class KeyboardSlotViewModel : ViewModelBase
{
    // private



    private readonly KeyboardSlotModel _model;
    private readonly KeyboardInputManager _inputManager;

    private readonly ObservableCollection<DeviceMenuItem> _deviceMenuItems;
    private readonly ObservableCollection<ConfigMenuItem> _configMenuItems;
    [ObservableProperty] private DeviceMenuItem _menuDevice;
    [ObservableProperty] private ConfigMenuItem _menuConfig;
    [ObservableProperty] private string _inputConfig;

    private ObservableCollection<DisplayKey> _keys;
    private Dictionary<ushort, List<DisplayKey>> _keyMap;
    [ObservableProperty] private double _canvasWidth;
    [ObservableProperty] private double _canvasHeight;

    private CallbackBlocker _callbackBlocker;



    // public



    public KeyboardSlotModel Model => _model;
    public ObservableCollection<DeviceMenuItem> DeviceMenuItems => _deviceMenuItems;
    public ObservableCollection<ConfigMenuItem> ConfigMenuItems => _configMenuItems;
    public ObservableCollection<DisplayKey> Keys => _keys;



    // events



    public event Action<string, Dictionary<ushort, KeybindModel>, Action<KeybindConfigModel>?>? AddConfigRequested;
    public event Action<KeybindConfigModel>? RemoveConfigRequested;
    public event Action<DisplayKey, KeybindConfigModel, int>? KeySelected;
    public event Action<IReadOnlyList<DisplayKey>?>? KeysDeselected;



    // constructor



    public KeyboardSlotViewModel(KeyboardSlotModel model, KeyboardInputManager inputManager, ObservableCollection<DeviceMenuItem> deviceMenuItems, ObservableCollection<ConfigMenuItem> configMenuItems)
    {
        _model = model;
        _inputManager = inputManager;
        _deviceMenuItems = deviceMenuItems;
        _configMenuItems = configMenuItems;

        _callbackBlocker = new();
        _callbackBlocker.Block();

        _menuDevice = new("null", null);
        _menuConfig = new("null", null);
        _inputConfig = "";
        _keys = [];
        _keyMap = [];

        InitKeys();

        SetMenuDevice(_model.SelectedDevice);
        SetMenuConfig(_model.SelectedConfig);
        SetKeybinds(_model.SelectedConfig?.Keybinds);

        SubscribeToKeyboardSlotModel(_model);
        SubscribeToKeybindConfigModel(_model.SelectedConfig);
        SubscribeToKeyboardInputManager(_inputManager);

        _callbackBlocker.Unblock();
    }



    // subscription helpers



    private void SubscribeToKeyboardSlotModel(KeyboardSlotModel model)
    {
        model.SelectedDeviceChanged += OnSelectedDeviceChanged;
        model.SelectedConfigChanged += OnSelectedConfigChanged;
    }



    private void UnsubscribeFromKeyboardSlotModel(KeyboardSlotModel model)
    {
        model.SelectedDeviceChanged -= OnSelectedDeviceChanged;
        model.SelectedConfigChanged -= OnSelectedConfigChanged;
    }



    private void SubscribeToKeybindConfigModel(KeybindConfigModel? model)
    {
        model?.KeyBound += OnKeyBound;
        model?.KeyUnbound += OnKeyUnbound;
    }



    private void UnsubscribeFromKeybindConfigModel(KeybindConfigModel? model)
    {
        model?.KeyBound -= OnKeyBound;
        model?.KeyUnbound -= OnKeyUnbound;
    }



    private void SubscribeToKeyboardInputManager(KeyboardInputManager manager)
    {
        manager.KeyPressed += OnKeyPressed;
        manager.KeyReleased += OnKeyReleased;
    }



    private void UnsubscribeFromKeyboardInputManager(KeyboardInputManager manager)
    {
        manager.KeyPressed -= OnKeyPressed;
        manager.KeyReleased -= OnKeyReleased;
    }



    // model calls



    partial void OnMenuDeviceChanged(DeviceMenuItem value)
    {
        if (_callbackBlocker.IsBlocked()) return;
        _model.TrySetSelectedDevice(value.Model);
    }



    partial void OnMenuConfigChanged(ConfigMenuItem value)
    {
        if (_callbackBlocker.IsBlocked()) return;
        _model.TrySetSelectedConfig(value.Model);
    }



    [RelayCommand]
    private void OnAddConfigRequested()
    {
        if (_callbackBlocker.IsBlocked()) return;
        var keybinds = new Dictionary<ushort, KeybindModel>();
        AddConfigRequested?.Invoke(InputConfig, keybinds, (configModel) => { InputConfig = ""; _model.TrySetSelectedConfig(configModel); });
    }



    [RelayCommand]
    private void OnRemoveConfigRequested()
    {
        if (_callbackBlocker.IsBlocked()) return;

        if (_model.SelectedConfig == null)
        {
            AppLog.WriteError($"Could not delete config - no config selected");
            return;
        }

        RemoveConfigRequested?.Invoke(_model.SelectedConfig);
    }



    public void OnActionDroppedOnKey(DisplayKey key, IAppAction action)
    {
        if (_callbackBlocker.IsBlocked()) return;

        if (_model.SelectedConfig == null)
        {
            AppLog.WriteError("Could not bind action to key - no config selected");
            return;
        }

        _model.SelectedConfig.BindActionToKey(key.VK, action);
    }



    public void OnKeyRightClicked(DisplayKey key)
    {
        if (_callbackBlocker.IsBlocked()) return;
        if (!key.IsBound) return;
        _model.SelectedConfig!.TryUnbindKey(key.VK);
    }



    internal void OnKeyLeftClicked(DisplayKey key)
    {
        if (_model.SelectedConfig == null)
        {
            AppLog.WriteError("Could not select key - no config selected");
            KeysDeselected?.Invoke(null);
            return;
        }

        var keybindModel = _model.SelectedConfig.Keybinds.GetValueOrDefault(key.VK);
        KeySelected?.Invoke(key, _model.SelectedConfig, _model.SlotID);
    }



    // model callbacks



    private void OnSelectedDeviceChanged(KeyboardDeviceModel? _, KeyboardDeviceModel? newDevice) => SetMenuDevice(newDevice);



    private void OnSelectedConfigChanged(KeybindConfigModel? oldModel, KeybindConfigModel? newModel)
    {
        UnsubscribeFromKeybindConfigModel(oldModel);
        SubscribeToKeybindConfigModel(newModel);

        SetMenuConfig(newModel);
        SetKeybinds(newModel?.Keybinds);
        KeysDeselected?.Invoke(_keys); 
    }



    private void OnKeyBound(ushort vk, KeybindModel _)
    {
        foreach (var key in _keyMap[vk])
            key.IsBound = true;
    }



    private void OnKeyUnbound(ushort vk, KeybindModel _)
    {
        foreach (var key in _keyMap[vk])
            key.IsBound = false;
    }



    // setters



    private void SetMenuDevice(KeyboardDeviceModel? device)
    {
        _callbackBlocker.Block();
        MenuDevice = _deviceMenuItems.FirstOrDefault(d => d.Model == device)!;
        _callbackBlocker.Unblock();
    }



    private void SetMenuConfig(KeybindConfigModel? config)
    {
        _callbackBlocker.Block();
        MenuConfig = _configMenuItems.FirstOrDefault(c => c.Model == config)!;
        _callbackBlocker.Unblock();
    }



    private void SetKeybinds(IReadOnlyDictionary<ushort, KeybindModel>? keybinds)
    {
        _callbackBlocker.Block();

        if (keybinds == null)
            foreach (var key in _keys)
                key.IsBound = false;
        else
            foreach (var key in _keys)
                key.IsBound = keybinds.ContainsKey(key.VK);

        _callbackBlocker.Unblock();
    }



    // key visuals



    private void OnKeyPressed(string devicePath, ushort vk)
    {
        if (devicePath != _model.SelectedDevice?.DevicePath) return;
        Dispatcher.UIThread.Post(() => SetKeyVisualState(vk, true));
    }



    private void OnKeyReleased(string devicePath, ushort vk)
    {
        if (devicePath != _model.SelectedDevice?.DevicePath) return;
        Dispatcher.UIThread.Post(() => SetKeyVisualState(vk, false));
    }



    private void SetKeyVisualState(ushort vk, bool isDown)
    {
        if (_keyMap.TryGetValue(vk, out var list))
            foreach (var key in list)
                key.IsPressed = isDown;
    }



    // keyboard display



    private void InitKeys()
    {
        var keys = DeviceDisplayHelper.InitKeyboardDisplayKeys(50, 6, out var width, out var height);

        CanvasWidth = width;
        CanvasHeight = height;

        foreach (var key in keys)
        {
            _keys.Add(key);

            if (!_keyMap.TryGetValue(key.VK, out var list))
            {
                list = [key];
                _keyMap.Add(key.VK, list);
            }
            else list.Add(key);
        }
    }



    // disposal



    public override void Dispose()
    {
        UnsubscribeFromKeyboardSlotModel(_model);
        UnsubscribeFromKeybindConfigModel(_model.SelectedConfig);
        UnsubscribeFromKeyboardInputManager(_inputManager);
    }
}