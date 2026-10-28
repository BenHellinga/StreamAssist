using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Core.Mvvm;
using StreamAssist.Core.Dragging;
using StreamAssist.Components.Keyboard.Model;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Components.OutputConsole.Model;
using System.Collections.Generic;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Keyboard.UI;



// view model for one key on the keyboard display
public partial class KeyViewModel : ViewModelBase, IDroppable, IEditButtons
{
    // private



    private KeybindConfigModel? _config;
    private BindableModel? _keybind;
    private KeybindViewModel? _keyConfigurator;

    private string _label;
    private ushort _vk;
    private double _baseX;
    private double _baseY;
    private double _baseW;
    private double _baseH;

    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    [ObservableProperty] private double _w;
    [ObservableProperty] private double _h;

    [ObservableProperty] private bool _isPressed;
    [ObservableProperty] private bool _isBound;
    [ObservableProperty] private bool _isDisabled;
    [ObservableProperty] private bool _isSelected;



    // public



    public string Label => _label;
    public ushort VK => _vk;



    // constructor



    public KeyViewModel(string label, ushort vk, double x, double y, double w, double h, KeybindViewModel? keyConfigurator = null)
    {
        _label = label;
        _vk = vk;
        _baseX = x;
        _baseY = y;
        _baseW = w;
        _baseH = h;
        _keyConfigurator = keyConfigurator;

        _x = -1;
        _y = -1;
        _w = -1;
        _h = -1;

        _isPressed = false;
        _isBound = false;
        _isDisabled = false;
        _isSelected = false;
    }



    // ui events



    public void OnPointerLeftClicked()
    {
        if (_config == null)
        {
            AppLog.WriteError("Could not select key - no config selected");
            return;
        }

        if (IsSelected)
        {
            _keyConfigurator?.DeselectKeys(null);
            IsSelected = false; 
        }
        else
        {
            _keyConfigurator?.SetSelectedKey(this, _keybind, _config);
            IsSelected = true;
        } 
    }



    [RelayCommand] private void OnToggleButtonPressed()
    {
        if (_keybind == null) return;
        _keybind.SetIsDisabled(!_keybind.IsDisabled);
    }



    [RelayCommand] private void OnRemoveButtonPressed() => _config?.UnbindKey(_vk);



    // drag drop



    // a key is a binding slot: it only takes whole bindings, and only when it doesn't already have one
    // (blocks are placed in the editor, not dropped on keys)
    public bool CanAcceptDropPayload(object payload) =>
        payload is BindableDragPayload bindablePayload
        && _config != null
        && bindablePayload.Bindable != _keybind
        && !IsBound;



    public void OnDroppedOn(object payload, object? zoneContext)
    {
        if (payload is not BindableDragPayload bindablePayload) return;
        if (_config == null) return;

        // a saved-but-empty bindable (grey key) is replaced
        if (_keybind != null) _config.UnbindKey(_vk);

        var copy = BindableModel.DeepCopy(bindablePayload.Bindable);
        if (!_config.TryBindKey(_vk, copy))
        {
            copy.Dispose();
            return;
        }

        // only remove the original once the copy is safely bound
        bindablePayload.RemoveFromSource?.Invoke();
    }



    public object? GetDragPayload()
    {
        if (_config == null || _keybind == null) return null;

        var config = _config;
        return new BindableDragPayload(_keybind, () => config.UnbindKey(_vk));
    }



    // model events



    private void OnKeyBound(ushort vk, BindableModel keybind)
    {
        if (vk != _vk) return;
        SetKeybind(keybind);
    }



    private void OnKeyUnbound(ushort vk, BindableModel _)
    {
        if (vk != _vk) return;
        SetKeybind(null);
    }



    private void OnDisabledChanged(bool isDisabled) => IsDisabled = isDisabled;



    private void OnKeybindChanged() => UpdateIsBound();



    // public api



    public void RecomputeSize(double scale, double padding)
    {
        X = _baseX * scale + padding;
        Y = _baseY * scale + padding;
        W = _baseW * scale;
        H = _baseH * scale;        
    }



    public void SetConfig(KeybindConfigModel? config)
    {
        UnsubscribeFromKeybindConfigModel(_config);

        _config = config;
        SetKeybind(_config?.Keybinds.GetValueOrDefault(_vk));
        SubscribeToKeybindConfigModel(_config);
    }



    // helpers



    private void SubscribeToKeybindConfigModel(KeybindConfigModel? config)
    {
        config?.KeyBound += OnKeyBound;
        config?.KeyUnbound += OnKeyUnbound;
    }



    private void UnsubscribeFromKeybindConfigModel(KeybindConfigModel? config)
    {
        config?.KeyBound -= OnKeyBound;
        config?.KeyUnbound -= OnKeyUnbound;
    }



    private void SubscribeToKeybindModel(BindableModel? keybind)
    {
        keybind?.IsDisabledChanged += OnDisabledChanged;
        keybind?.Changed += OnKeybindChanged;
    }



    private void UnsubscribeFromKeybindModel(BindableModel? keybind)
    {
        keybind?.IsDisabledChanged -= OnDisabledChanged;
        keybind?.Changed -= OnKeybindChanged;
    }



    private void SetKeybind(BindableModel? keybind)
    {
        UnsubscribeFromKeybindModel(_keybind);

        _keybind = keybind;

        UpdateIsBound();
        IsDisabled = _keybind?.IsDisabled ?? false;

        SubscribeToKeybindModel(_keybind);
    }



    // a key shows as bound when its slot has a block in it
    private void UpdateIsBound() => IsBound = _keybind?.HasBlock ?? false;



    // disposal



    public override void Dispose()
    {
        UnsubscribeFromKeybindConfigModel(_config);
        UnsubscribeFromKeybindModel(_keybind);
    }
}
