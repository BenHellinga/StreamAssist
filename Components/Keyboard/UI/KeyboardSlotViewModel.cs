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
    [ObservableProperty] private DeviceMenuItem _menuDevice;
    [ObservableProperty] private string _menuLayout;

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
    public ObservableCollection<KeyViewModel> Keys => _keys;
    public IReadOnlyList<string> Layouts => KeyboardSlotModel.Layouts;



    // constructor



    public KeyboardSlotViewModel(KeyboardSlotModel slot, KeyboardInputManager inputManager,
                                 ObservableCollection<DeviceMenuItem> deviceMenuItems,
                                 KeybindViewModel? keyConfigurator = null)
    {
        _slot = slot;
        _inputManager = inputManager;
        _keyConfigurator = keyConfigurator;
        _deviceMenuItems = deviceMenuItems;

        _menuDevice = new("null", null);
        _menuLayout = _slot.Layout;

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
        SetKeyConfigs(_slot.SelectedConfig);

        SubscribeToKeyboardSlotModel(_slot);
        SubscribeToKeyboardInputManager(_inputManager);

        _callbackBlocker.Unblock();
    }



    // ui events



    partial void OnMenuLayoutChanged(string value)
    {
        if (_callbackBlocker.IsBlocked() || value == null) return;
        if (!_slot.TrySetLayout(value)) MenuLayout = _slot.Layout;
    }



    partial void OnMenuDeviceChanged(DeviceMenuItem value)
    {
        if (_callbackBlocker.IsBlocked() || value == null) return; // null while the dropdown's list is being changed
        _slot.TrySetSelectedDevice(value.Model);
    }



    // model events



    private void OnSelectedDeviceChanged(InputDeviceModel? _, InputDeviceModel? newDevice) => SetMenuDevice(newDevice);



    // another picture: the keys are made again (a selected key is let go, binds show on whichever keys have them)
    private void OnLayoutChanged(string layout)
    {
        _callbackBlocker.Block();
        MenuLayout = layout;
        _callbackBlocker.Unblock();

        _keyConfigurator?.DeselectKeys(_keys);
        foreach (var key in _keys) key.Dispose();
        _keys.Clear();
        _keyMap.Clear();

        InitKeys();
        SetKeyConfigs(_slot.SelectedConfig);
        RecomputeDisplaySize();
    }



    // the tab switched configs: this keyboard's keys in that config
    private void OnSelectedConfigChanged(KeybindConfigModel? _, KeybindConfigModel? newConfig)
    {
        _keyConfigurator?.DeselectKeys(_keys);
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
        slot.LayoutChanged += OnLayoutChanged;
    }



    private void UnsubscribeFromKeyboardSlotModel(KeyboardSlotModel slot)
    {
        slot.SelectedDeviceChanged -= OnSelectedDeviceChanged;
        slot.SelectedConfigChanged -= OnSelectedConfigChanged;
        slot.LayoutChanged -= OnLayoutChanged;
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



    // after the keyboard list changed: show the slot's keyboard (or none) again
    public void ShowSelectedDevice() => SetMenuDevice(_slot.SelectedDevice);



    private void SetMenuDevice(InputDeviceModel? device)
    {
        _callbackBlocker.Block();
        MenuDevice = _deviceMenuItems.FirstOrDefault(d => d.Model == device)!;
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
        (var keyData, _baseWidth, _baseHeight) = _slot.Layout == KeyboardSlotModel.AllKeysLayout
            ? DeviceDisplayHelper.InitGenericDisplayKeys(25, 3)
            : DeviceDisplayHelper.InitKeyboardDisplayData(25, 3);

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
