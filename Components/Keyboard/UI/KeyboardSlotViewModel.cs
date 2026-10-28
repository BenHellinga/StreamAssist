using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Core.Mvvm;
using StreamAssist.Core.Utilities;
using StreamAssist.Components.Keyboard.Background;
using StreamAssist.Components.Keyboard.Model;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Keyboard.UI;



// view model to modify a KeyboardSlotModel object
public partial class KeyboardSlotViewModel : ViewModelBase
{
    // private



    private readonly KeyboardSlotModel _slot;
    private readonly KeyboardInputManager _inputManager;
    private readonly KeybindViewModel? _keyConfigurator;

    private readonly ObservableCollection<DeviceMenuItem> _deviceMenuItems;
    private readonly ObservableCollection<ConfigMenuItem> _configMenuItems;
    [ObservableProperty] private DeviceMenuItem _menuDevice;
    [ObservableProperty] private ConfigMenuItem _menuConfig;
    [ObservableProperty] private string _inputConfig;

    private double _baseWidth;
    private double _baseHeight;
    private double _pendingWidth;
    private IDisposable? _pendingResize;
    [ObservableProperty] private double _currentWidth;
    [ObservableProperty] private double _currentHeight;
    private ObservableCollection<KeyViewModel> _keys;
    private Dictionary<ushort, List<KeyViewModel>> _keyMap;

    private CallbackBlocker _callbackBlocker;



    // public



    public ObservableCollection<DeviceMenuItem> DeviceMenuItems => _deviceMenuItems;
    public ObservableCollection<ConfigMenuItem> ConfigMenuItems => _configMenuItems;
    public ObservableCollection<KeyViewModel> Keys => _keys;



    // events



    public event Action<string, Dictionary<ushort, BindableModel>, Action<KeybindConfigModel>?>? CreateConfigRequested;
    public event Action<KeybindConfigModel>? DeleteConfigRequested;



    // constructor



    public KeyboardSlotViewModel(KeyboardSlotModel slot, KeyboardInputManager inputManager,
                                 ObservableCollection<DeviceMenuItem> deviceMenuItems, ObservableCollection<ConfigMenuItem> configMenuItems,
                                 KeybindViewModel? keyConfigurator = null)
    {
        _slot = slot;
        _inputManager = inputManager;
        _keyConfigurator = keyConfigurator;
        _deviceMenuItems = deviceMenuItems;
        _configMenuItems = configMenuItems;

        _menuDevice = new("null", null);
        _menuConfig = new("null", null);
        _inputConfig = "";

        _baseWidth = -1;
        _baseHeight = -1;
        _pendingWidth = -1;
        _pendingResize = null;
        _currentWidth = -1;
        _currentHeight = -1;
        _keys = [];
        _keyMap = [];

        _callbackBlocker = new();

        _callbackBlocker.Block();

        InitKeys();

        SetMenuDevice(_slot.SelectedDevice);
        SetMenuConfig(_slot.SelectedConfig);
        SetKeyConfigs(_slot.SelectedConfig);

        SubscribeToKeyboardSlotModel(_slot);
        SubscribeToKeyboardInputManager(_inputManager);

        _callbackBlocker.Unblock();
    }



    // ui events



    partial void OnMenuDeviceChanged(DeviceMenuItem value)
    {
        if (_callbackBlocker.IsBlocked()) return;
        _slot.TrySetSelectedDevice(value.Model);
    }



    partial void OnMenuConfigChanged(ConfigMenuItem value)
    {
        if (_callbackBlocker.IsBlocked()) return;
        _slot.TrySetSelectedConfig(value.Model);
    }



    [RelayCommand] private void OnCreateConfigPressed()
    {
        if (_callbackBlocker.IsBlocked()) return;

        // copy current config's keybinds
        var json = JsonSerializer.Serialize(_slot.SelectedConfig?.Keybinds);
        var keybinds = JsonSerializer.Deserialize<Dictionary<ushort, BindableModel>>(json) ?? [];

        CreateConfigRequested?.Invoke(
            InputConfig,
            keybinds,
            (configModel) =>
            {
                InputConfig = "";
                _slot.TrySetSelectedConfig(configModel);
            }
        );
    }



    [RelayCommand] private void OnDeleteConfigPressed()
    {
        if (_callbackBlocker.IsBlocked()) return;

        if (_slot.SelectedConfig == null)
        {
            AppLog.WriteError($"Could not delete config - no config selected");
            return;
        }

        DeleteConfigRequested?.Invoke(_slot.SelectedConfig);
    }



    // model events



    private void OnSelectedDeviceChanged(InputDeviceModel? _, InputDeviceModel? newDevice) => SetMenuDevice(newDevice);



    private void OnSelectedConfigChanged(KeybindConfigModel? _, KeybindConfigModel? newConfig)
    {
        _keyConfigurator?.DeselectKeys(_keys);
        SetMenuConfig(newConfig);
        SetKeyConfigs(newConfig);
    }



    // background events



    private void OnKeyPressed(string devicePath, ushort vk)
    {
        if (devicePath != _slot.SelectedDevice?.DevicePath) return;
        Dispatcher.UIThread.Post(() => SetKeyVisualState(vk, true));
    }



    private void OnKeyReleased(string devicePath, ushort vk)
    {
        if (devicePath != _slot.SelectedDevice?.DevicePath) return;
        Dispatcher.UIThread.Post(() => SetKeyVisualState(vk, false));
    }



    // public api



    public bool RepresentsModel(KeyboardSlotModel slot) => _slot == slot;



    public void SetDisplayWidth(double width)
    {
        _pendingWidth = width;
        if (_pendingResize != null) return;

        _pendingResize = DispatcherTimer.RunOnce(() =>
        {
            _pendingResize = null;
            RecomputeDisplaySize();
        }, TimeSpan.FromMilliseconds(1));
    }



    // helpers



    private void SubscribeToKeyboardSlotModel(KeyboardSlotModel slot)
    {
        slot.SelectedDeviceChanged += OnSelectedDeviceChanged;
        slot.SelectedConfigChanged += OnSelectedConfigChanged;
    }



    private void UnsubscribeFromKeyboardSlotModel(KeyboardSlotModel slot)
    {
        slot.SelectedDeviceChanged -= OnSelectedDeviceChanged;
        slot.SelectedConfigChanged -= OnSelectedConfigChanged;
    }



    private void SubscribeToKeyboardInputManager(KeyboardInputManager manager)
    {
        manager.KeyDown += OnKeyPressed;
        manager.KeyUp += OnKeyReleased;
    }



    private void UnsubscribeFromKeyboardInputManager(KeyboardInputManager manager)
    {
        manager.KeyDown -= OnKeyPressed;
        manager.KeyUp -= OnKeyReleased;
    }



    private void SetMenuDevice(InputDeviceModel? device)
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



    private void SetKeyConfigs(KeybindConfigModel? config)
    {
        foreach (var key in _keys)
            key.SetConfig(config);
    }



    private void SetKeyVisualState(ushort vk, bool isDown)
    {
        if (_keyMap.TryGetValue(vk, out var list))
            foreach (var key in list)
                key.IsPressed = isDown;
    }



    private void InitKeys()
    {
        (var keyData, _baseWidth, _baseHeight) = DeviceDisplayHelper.InitKeyboardDisplayData(25, 3);

        CurrentWidth = _baseWidth;
        CurrentHeight = _baseHeight;

        foreach (var d in keyData)
        {
            var key = new KeyViewModel(d.Label, d.VK, d.X, d.Y, d.W, d.H, _keyConfigurator);

            _keys.Add(key);

            if (!_keyMap.TryGetValue(d.VK, out var list))
            {
                list = [key];
                _keyMap.Add(d.VK, list);
            }
            else list.Add(key);
        }
    }



    private void RecomputeDisplaySize()
    {
        if (_pendingWidth <= 10 || _baseWidth <= 0) return;

        var padding = 1;
        var usableWidth = _pendingWidth - padding * 2;
        var scale = usableWidth / _baseWidth;

        foreach (var key in _keys)
            key.RecomputeSize(scale, padding);

        CurrentWidth = _baseWidth * scale + padding * 2;
        CurrentHeight = _baseHeight * scale + padding * 2;
    }



    // disposal



    public override void Dispose()
    {
        UnsubscribeFromKeyboardSlotModel(_slot);
        UnsubscribeFromKeyboardInputManager(_inputManager);

        _pendingResize?.Dispose();

        foreach (var key in _keys)
            key.Dispose();
    }
}
