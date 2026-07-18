using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.AppAction.Model;
using StreamAssist.Components.AppAction.UI;
using StreamAssist.Components.Core;
using StreamAssist.Components.Keyboard.Model;
using StreamAssist.Components.Main.Model;

namespace StreamAssist.Components.Keyboard.UI;



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
            new("On Pressed", () => new ActionGroupViewModel(_keybind!.OnPressed)),
            new("On Released", () => new ActionGroupViewModel(_keybind!.OnReleased)),
            new("On Repeated", () => new ActionGroupViewModel(_keybind!.OnRepeated)),
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

        UnsubscribeFromAllActionGroupModels(_keybind);
        _keybind = keybind;
        SubscribeToAllActionGroupModels(_keybind);
        SetContent(SelectedTab);
    }



    private void OnKeyUnbound(ushort vk, KeybindModel keybind)
    {
        if (vk != _key?.VK) return;

        UnsubscribeFromAllActionGroupModels(_keybind);
        _keybind = new();
        SubscribeToAllActionGroupModels(_keybind);
        SetContent(SelectedTab);
    }



    private void OnFirstActionAdded(IAppAction _)
    {
        if (1 == _keybind!.OnPressed.Actions.Count +
                 _keybind!.OnReleased.Actions.Count + 
                 _keybind!.OnRepeated.Actions.Count)
            _config?.TryBindKey(_key!.VK, _keybind);
    }



    private void OnLastActionRemoved(IAppAction _)
    {
        if (0 == _keybind!.OnPressed.Actions.Count +
                 _keybind!.OnReleased.Actions.Count + 
                 _keybind!.OnRepeated.Actions.Count)
            _config?.TryUnbindKey(_key!.VK);
    }



    // public api



    public void SetSelectedKey(KeyViewModel key, KeybindModel? keybind, KeybindConfigModel? configModel)
    {
        _key?.Deselect();

        _key = key;
        _keybind = keybind ?? new();
        _config = configModel;

        SetContent(SelectedTab);
        Label = $"Key: {key.Label.Replace('\n', '-')}   Config: {_config?.Name}";
        IsKeySelected = true;

        SubscribeToKeybindConfigModel(_config);
        SubscribeToAllActionGroupModels(_keybind);
    }



    public void DeselectKeys(IReadOnlyList<KeyViewModel>? keys)
    {
        if (keys != null && !keys.Contains(_key)) return;

        UnsubscribeFromKeybindConfigModel(_config);
        UnsubscribeFromAllActionGroupModels(_keybind);

        IsKeySelected = false;
        SetContent(null);

        _key?.Deselect();
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



    private void SubscribeToAllActionGroupModels(KeybindModel? keybind)
    {
        keybind?.OnPressed.FirstActionAdded += OnFirstActionAdded;
        keybind?.OnReleased.FirstActionAdded += OnFirstActionAdded;
        keybind?.OnRepeated.FirstActionAdded += OnFirstActionAdded;

        keybind?.OnPressed.LastActionRemoved += OnLastActionRemoved;
        keybind?.OnReleased.LastActionRemoved += OnLastActionRemoved;
        keybind?.OnRepeated.LastActionRemoved += OnLastActionRemoved;
    }



    private void UnsubscribeFromAllActionGroupModels(KeybindModel? keybind)
    {
        keybind?.OnPressed.FirstActionAdded -= OnFirstActionAdded;
        keybind?.OnReleased.FirstActionAdded -= OnFirstActionAdded;
        keybind?.OnRepeated.FirstActionAdded -= OnFirstActionAdded;

        keybind?.OnPressed.LastActionRemoved -= OnLastActionRemoved;
        keybind?.OnReleased.LastActionRemoved -= OnLastActionRemoved;
        keybind?.OnRepeated.LastActionRemoved -= OnLastActionRemoved;
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
        
        ActiveContent?.Dispose();
    }
}