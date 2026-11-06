using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Core.Mvvm;
using StreamAssist.Core.Utilities;
using StreamAssist.Components.Input.Background;
using StreamAssist.Components.Input.Model;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Blocks.UI;

namespace StreamAssist.Components.Input.UI;



// one input device box on the Input tab: its device dropdown (only devices of its layout's kind), its picture (keys,
// mouse or controller buttons, a body outline behind a mouse's or controller's), and its corner buttons (copy,
// collapse, on / off, drag to reorder, delete), which go through the slot and the list
public partial class InputSlotViewModel : ViewModelBase, IEditButtons, ICommitOnLeave
{
    // private



    private readonly InputSlotModel _slot;
    private readonly InputListModel _list;
    private readonly DeviceInputManager _inputManager;
    private readonly KeybindViewModel? _keyConfigurator;

    private readonly DeviceMenus _deviceMenus; // shared by every box
    [ObservableProperty] private DeviceMenuItem _menuDevice;
    [ObservableProperty] private bool _isDisabled;
    [ObservableProperty] private bool _isCollapsed;

    private double _baseWidth;
    private double _baseHeight;
    private double _pendingWidth;
    private IDisposable? _pendingResize;
    [ObservableProperty] private double _currentWidth;
    [ObservableProperty] private double _currentHeight;
    [ObservableProperty] private bool _hasBody; // a mouse or controller: an outline behind its buttons
    [ObservableProperty] private Geometry? _bodyGeometry; // that outline at the picture's size

    // a microphone box: its level (dB, the meter shows -60 to 0), threshold (the slider) and hold (applied on leave)
    [ObservableProperty] private double _micLevel;
    [ObservableProperty] private double _micThreshold;
    [ObservableProperty] private string _micHoldInput = "";
    [ObservableProperty] private bool _showMicControls;
    private ObservableCollection<KeyViewModel> _keys;
    private Dictionary<ushort, List<KeyViewModel>> _keyMap;

    private CallbackBlocker _callbackBlocker;



    // public



    public ObservableCollection<DeviceMenuItem> DeviceMenuItems => _deviceMenus.For(_slot.DeviceKind);
    public ObservableCollection<KeyViewModel> Keys => _keys;
    public bool CanCollapse => true;
    public bool CanCopy => true;

    // the box (its layout, on/off) and its keys' bindings in the config in use, pasted on the ghost slot at the end
    [RelayCommand] private void OnCopyButtonPressed() =>
        BlockClipboard.Copy(InputDeviceCopy.Kind, new InputDeviceCopy(_slot.Layout, _slot.IsDisabled, _slot.SelectedConfig?.Keybinds));
    public bool CanToggle => true;



    // constructor



    public InputSlotViewModel(InputSlotModel slot, InputListModel list, DeviceInputManager inputManager,
                                 DeviceMenus deviceMenus,
                                 KeybindViewModel? keyConfigurator = null)
    {
        _slot = slot;
        _list = list;
        _inputManager = inputManager;
        _keyConfigurator = keyConfigurator;
        _deviceMenus = deviceMenus;

        _menuDevice = new("null", null);
        _isDisabled = _slot.IsDisabled;
        _isCollapsed = _slot.IsCollapsed;

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

        RefreshMic();

        SubscribeToKeyboardSlotModel(_slot);
        SubscribeToKeyboardInputManager(_inputManager);

        _callbackBlocker.Unblock();
    }



    // ui events



    [RelayCommand] private void OnToggleButtonPressed() => _slot.SetIsDisabled(!_slot.IsDisabled);



    [RelayCommand] private void OnCollapseButtonPressed() => _slot.SetIsCollapsed(!_slot.IsCollapsed);



    [RelayCommand] private void OnRemoveButtonPressed() => _list.TryDeleteSlot(_slot);



    public object? GetDragPayload() => new InputSlotDragPayload(_slot);



    partial void OnMenuDeviceChanged(DeviceMenuItem value)
    {
        if (_callbackBlocker.IsBlocked() || value == null) return; // null while the dropdown's list is being changed
        _slot.TrySetSelectedDevice(value.Model);
    }



    // model events



    private void OnSelectedDeviceChanged(InputDeviceModel? _, InputDeviceModel? newDevice) => SetMenuDevice(newDevice);



    private void OnSlotIsDisabledChanged(bool isDisabled) => IsDisabled = isDisabled;



    private void OnSlotIsCollapsedChanged(bool isCollapsed)
    {
        IsCollapsed = isCollapsed;
        RefreshMic();
    }



    // a microphone box



    private void OnSlotMicLevelChanged(double level) => MicLevel = Math.Clamp(level, -60, 0);



    private void OnSlotMicSettingsChanged() => RefreshMic();



    private void OnSlotIsTalkingChanged(bool isTalking) => SetKeyVisualState(InputDevices.Talking, isTalking);



    // the slider goes straight to the model (a refused value is put back)
    partial void OnMicThresholdChanged(double value)
    {
        if (_callbackBlocker.IsBlocked()) return;
        if (!_slot.TrySetMicThreshold(Math.Round(value))) RefreshMic();
    }



    // the hold box was left (Enter or clicking away): a number of ms, a refused one is put back
    public void Commit()
    {
        if (int.TryParse(MicHoldInput.Trim(), out var hold)) _slot.TrySetMicHold(hold);
        RefreshMic();
    }



    private void RefreshMic()
    {
        _callbackBlocker.Block();
        ShowMicControls = _slot.Layout == InputSlotModel.MicrophoneLayout && !_slot.IsCollapsed;
        MicThreshold = _slot.MicThreshold;
        MicHoldInput = _slot.MicHold.ToString();
        MicLevel = Math.Clamp(_slot.MicLevel, -60, 0);
        _callbackBlocker.Unblock();
    }



    // another picture: the keys are made again (a selected key is let go, binds show on whichever keys have them)
    private void OnLayoutChanged(string layout)
    {
        _keyConfigurator?.DeselectKeys(_keys);
        foreach (var key in _keys) key.Dispose();
        _keys.Clear();
        _keyMap.Clear();

        InitKeys();
        SetKeyConfigs(_slot.SelectedConfig);
        RecomputeDisplaySize();
        OnPropertyChanged(nameof(DeviceMenuItems)); // another kind of device
        SetMenuDevice(_slot.SelectedDevice);
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



    public bool RepresentsModel(InputSlotModel slot) => _slot == slot;



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



    private void SubscribeToKeyboardSlotModel(InputSlotModel slot)
    {
        slot.SelectedDeviceChanged += OnSelectedDeviceChanged;
        slot.SelectedConfigChanged += OnSelectedConfigChanged;
        slot.LayoutChanged += OnLayoutChanged;
        slot.IsDisabledChanged += OnSlotIsDisabledChanged;
        slot.IsCollapsedChanged += OnSlotIsCollapsedChanged;
        slot.MicLevelChanged += OnSlotMicLevelChanged;
        slot.MicSettingsChanged += OnSlotMicSettingsChanged;
        slot.IsTalkingChanged += OnSlotIsTalkingChanged;
    }



    private void UnsubscribeFromKeyboardSlotModel(InputSlotModel slot)
    {
        slot.SelectedDeviceChanged -= OnSelectedDeviceChanged;
        slot.SelectedConfigChanged -= OnSelectedConfigChanged;
        slot.LayoutChanged -= OnLayoutChanged;
        slot.IsDisabledChanged -= OnSlotIsDisabledChanged;
        slot.IsCollapsedChanged -= OnSlotIsCollapsedChanged;
        slot.MicLevelChanged -= OnSlotMicLevelChanged;
        slot.MicSettingsChanged -= OnSlotMicSettingsChanged;
        slot.IsTalkingChanged -= OnSlotIsTalkingChanged;
    }



    private void SubscribeToKeyboardInputManager(DeviceInputManager manager)
    {
        manager.KeyDown += OnKeyPressed;
        manager.KeyUp += OnKeyReleased;
    }



    private void UnsubscribeFromKeyboardInputManager(DeviceInputManager manager)
    {
        manager.KeyDown -= OnKeyPressed;
        manager.KeyUp -= OnKeyReleased;
    }



    // after the keyboard list changed: show the slot's keyboard (or none) again
    public void ShowSelectedDevice() => SetMenuDevice(_slot.SelectedDevice);



    private void SetMenuDevice(InputDeviceModel? device)
    {
        _callbackBlocker.Block();
        MenuDevice = DeviceMenuItems.FirstOrDefault(d => d.Model == device)!;
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
        (var keyData, _baseWidth, _baseHeight) = DeviceDisplayHelper.DisplayData(_slot.Layout, 25, 3);

        CurrentWidth = _baseWidth;
        CurrentHeight = _baseHeight;
        HasBody = DeviceDisplayHelper.BodyPath(_slot.Layout) != null;
        SetBody(1, 0);

        foreach (var d in keyData)
        {
            var key = new KeyViewModel(d.Label, d.VK, d.X, d.Y, d.W, d.H, _keyConfigurator, d.Name, d.Corners);

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
        var scale = Math.Min(usableWidth / _baseWidth, DeviceDisplayHelper.MaxScale(_slot.Layout));

        foreach (var key in _keys)
            key.RecomputeSize(scale, padding);

        CurrentWidth = _baseWidth * scale + padding * 2;
        CurrentHeight = _baseHeight * scale + padding * 2;
        SetBody(scale, padding);
    }



    // the body outline at this size: its path scaled like the keys (a new geometry, so the line stays its own width)
    private void SetBody(double scale, double padding)
    {
        if (DeviceDisplayHelper.BodyPath(_slot.Layout) is not { } path) return;

        var geometry = Geometry.Parse(path);
        geometry.Transform = new MatrixTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(padding, padding));
        BodyGeometry = geometry;
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
