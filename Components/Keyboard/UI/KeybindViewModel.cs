using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.AppAction.UI;
using StreamAssist.Core.Mvvm;
using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Components.Keyboard.UI;



// view model to modify a KeybindModel object
public partial class KeybindViewModel : ViewModelBase
{
    // private



    private KeyViewModel? _key;
    private KeybindModel? _keybind;
    private KeybindConfigModel? _config;

    private ObservableCollection<ViewModelTabItem> _tabs;
    [ObservableProperty] private ViewModelTabItem _selectedTab;
    [ObservableProperty] private ViewModelBase? _activeContent;

    [ObservableProperty] private bool _isKeySelected;
    [ObservableProperty] private string _label;



    // public



    public ObservableCollection<ViewModelTabItem> Tabs => _tabs;



    // constructor



    public KeybindViewModel()
    {
        _key = null;
        _keybind = null;
        _config = null;

        _tabs = [
            new("On Pressed", () => new ActionGroupViewModel(_keybind!.OnPressed, g => _keybind!.TryBindTrigger(KeybindModel.TriggerType.OnPressed, g))),
            new("On Released", () => new ActionGroupViewModel(_keybind!.OnReleased, g => _keybind!.TryBindTrigger(KeybindModel.TriggerType.OnReleased, g))),
            new("On Repeated", () => new ActionGroupViewModel(_keybind!.OnRepeated, g => _keybind!.TryBindTrigger(KeybindModel.TriggerType.OnRepeated, g))),
        ];
        _selectedTab = _tabs[0];
        _activeContent = null;

        _isKeySelected = false;
        _label = "null";
    }



    // ui events



    partial void OnSelectedTabChanged(ViewModelTabItem value) => SetContent(value);



    // model events



    private void OnKeyBound(ushort vk, KeybindModel keybind)
    {
        if (vk != _key?.VK) return;

        // the selected key was just bound to a keybind
        UnsubscribeFromKeybindModel(_keybind);
        SubscribeToKeybindModel(keybind);
        _keybind = keybind;
        SetContent(SelectedTab);
    }



    private void OnKeyUnbound(ushort vk, KeybindModel keybind)
    {
        if (vk != _key?.VK) return;

        // the selected key was just unbound
        _keybind = new();
        UnsubscribeFromKeybindModel(keybind);
        SubscribeToKeybindModel(_keybind);
        SetContent(SelectedTab);
    }



    private void OnFirstTriggerBound() => _config?.TryBindKey(_key!.VK, _keybind!);



    // public api



    public void SetSelectedKey(KeyViewModel key, KeybindModel? keybind, KeybindConfigModel config)
    {
        // a new key was selected
        UnsubscribeFromKeybindConfigModel(_config);
        UnsubscribeFromKeybindModel(_keybind);

        _key?.IsSelected = false;

        _key = key;
        _keybind = keybind ?? new();
        _config = config;

        SubscribeToKeybindConfigModel(config);
        SubscribeToKeybindModel(keybind);

        SetContent(SelectedTab);
        Label = $"Key: {key.Label.Replace('\n', '-')}     Config: {_config.Name}";
        IsKeySelected = true;
    }



    public void DeselectKeys(IReadOnlyList<KeyViewModel>? keys)
    {
        if (keys != null && !keys.Contains(_key)) return;

        // the selected key was just deselected
        UnsubscribeFromKeybindConfigModel(_config);
        UnsubscribeFromKeybindModel(_keybind);

        IsKeySelected = false;
        SetContent(null);

        _key?.IsSelected = false;
        _key = null;
        _keybind = null;
        _config = null;
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



    private void SubscribeToKeybindModel(KeybindModel? keybind)
    {
        keybind?.FirstTriggerBound += OnFirstTriggerBound;
    }



    private void UnsubscribeFromKeybindModel(KeybindModel? keybind)
    {
        keybind?.FirstTriggerBound -= OnFirstTriggerBound;
    }



    private void SetContent(ViewModelTabItem? content)
    {
        ViewModelBase? oldContent = ActiveContent;
        ActiveContent = content?.CreateTabViewModel();
        oldContent?.Dispose();
    }



    // disposal



    public override void Dispose()
    {
        UnsubscribeFromKeybindConfigModel(_config);
        UnsubscribeFromKeybindModel(_keybind);
        
        ActiveContent?.Dispose();
    }
}