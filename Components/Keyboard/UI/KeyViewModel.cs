using System;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Core;
using StreamAssist.Components.Core.Dragging;
using StreamAssist.Components.Keyboard.Model;
using StreamAssist.Components.AppAction.UI;
using StreamAssist.Components.Main.Model;
using System.Collections.Generic;

namespace StreamAssist.Components.Keyboard.UI;



public partial class KeyViewModel : ViewModelBase, IDraggable, IDroppable
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
    [ObservableProperty] private bool _isEnabled;
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

        SubscribeToKeybindConfigModel(_config);
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

        payload.TryRemoveFromSource();

        if (_keybind == null)
        {
            var keybind = new KeybindModel();
            keybind.OnPressed.AddAction(payload.Action);
            _config.TryBindKey(_vk, keybind);
        }
        else _keybind.OnPressed.AddAction(payload.Action);
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



    [RelayCommand]
    private void OnToggleEnabledPressed()
    {
        if (_keybind == null)
        {
            AppLog.WriteError("Could not toggle keybind - key not bound");
            return;    
        }

        _keybind.TrySetIsEnabled(!IsEnabled);
    }



    [RelayCommand]
    private void OnUnbindPressed() => _config?.TryUnbindKey(_vk);



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



    private void OnEnabledChanged(bool isEnabled) => IsEnabled = isEnabled;



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
        keybind?.IsEnabledChanged += OnEnabledChanged;
    }



    private void UnsubscribeFromKeybindModel(KeybindModel? keybind)
    {
        keybind?.IsEnabledChanged -= OnEnabledChanged;
    }



    private void SetKeybind(KeybindModel? keybind)
    {
        UnsubscribeFromKeybindModel(_keybind);

        _keybind = keybind;
        
        IsBound = _keybind != null;
        IsEnabled = _keybind?.IsEnabled ?? true;

        SubscribeToKeybindModel(_keybind);
    }




    // disposal



    public override void Dispose()
    {
        UnsubscribeFromKeybindConfigModel(_config);
        UnsubscribeFromKeybindModel(_keybind);
    }
}
