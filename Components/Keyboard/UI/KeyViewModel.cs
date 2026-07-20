using System;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Core;
using StreamAssist.Components.Core.Dragging;
using StreamAssist.Components.Keyboard.Model;
using StreamAssist.Components.AppAction.UI;
using StreamAssist.Components.Main.Model;
using System.Collections.Generic;
using StreamAssist.Components.AppAction.Model;

namespace StreamAssist.Components.Keyboard.UI;



public partial class KeyViewModel : ViewModelBase, IDroppable, IActionUIButtons
{
    // private



    private KeybindConfigModel? _config;
    private KeybindModel? _keybind;
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



    // drag drop



    public bool CanAcceptDropPayload(object payload) => payload is ActionDragPayload or KeybindDragPayload;



    public void OnDroppedOn(object payload, object? zoneContext)
    {
        if (payload is ActionDragPayload actionPayload)
            OnActionDroppedOn(actionPayload);

        else if (payload is KeybindDragPayload keybindPayload)
            OnKeybindDroppedOn(keybindPayload);
    }



    private void OnActionDroppedOn(ActionDragPayload payload)
    {
        if (_config == null)
        {
            AppLog.WriteError("Could not bind action to key - no config selected");
            return;
        }

        var copy = ActionModel.DeepCopy(payload.Action);

        if (_keybind == null)
        {
            var keybind = new KeybindModel();
            keybind.OnPressed.AddAction(copy);
            _config.TryBindKey(_vk, keybind);
        }
        else _keybind.OnPressed.AddAction(copy);

        payload.RemoveFromSource?.Invoke();
    }



    private void OnKeybindDroppedOn(KeybindDragPayload payload)
    {
        if (_config == null)
        {
            AppLog.WriteError("Could not bind action to key - no config selected");
            return;
        }

        if (_keybind != null)
        {
            AppLog.WriteError("Could not bind action group to key - key is already bound to an action group");
            return;
        }

        payload.RemoveFromSource();
        _config.TryBindKey(_vk, payload.keybind);
    }



    public object? GetDragPayload()
    {
        if (_config == null || _keybind == null) return null;
        return new KeybindDragPayload(_keybind, () => _config.TryUnbindKey(_vk));
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
        if (_keybind == null)
        {
            AppLog.WriteError("Could not toggle keybind - key not bound");
            return;    
        }

        _keybind.SetIsDisabled(!_keybind.IsDisabled);
    }



    [RelayCommand] private void OnRemoveButtonPressed() => _config?.TryUnbindKey(_vk);



    // model events



    private void OnKeyBound(ushort vk, KeybindModel keybind)
    {
        if (vk != _vk) return;
        SetKeybind(keybind);
    }



    private void OnKeyUnbound(ushort vk, KeybindModel _)
    {
        if (vk != _vk) return;
        SetKeybind(null);
    }



    private void OnDisabledChanged(bool isDisabled) => IsDisabled = isDisabled;



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



    public void Deselect() => IsSelected = false;



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



    private void SubscribeToKeybindModel(KeybindModel? keybind)
    {
        keybind?.IsDisabledChanged += OnDisabledChanged;
    }



    private void UnsubscribeFromKeybindModel(KeybindModel? keybind)
    {
        keybind?.IsDisabledChanged -= OnDisabledChanged;
    }



    private void SetKeybind(KeybindModel? keybind)
    {
        UnsubscribeFromKeybindModel(_keybind);

        _keybind = keybind;
        
        IsBound = _keybind != null;
        IsDisabled = _keybind?.IsDisabled ?? false;

        SubscribeToKeybindModel(_keybind);
    }




    // disposal



    public override void Dispose()
    {
        UnsubscribeFromKeybindConfigModel(_config);
        UnsubscribeFromKeybindModel(_keybind);
    }
}
