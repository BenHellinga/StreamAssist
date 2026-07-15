using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.AppAction.UI;
using StreamAssist.Components.Core;
using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Components.Keyboard.UI;



public partial class KeybindViewModel : ViewModelBase
{
    // private



    [ObservableProperty] private bool _isKeySelected;
    private DisplayKey? _key;
    private KeybindModel? _keybindModel;
    private KeybindConfigModel? _configModel;

    [ObservableProperty] private string _label;
    private ObservableCollection<ViewModelTabItem> _tabs;
    [ObservableProperty] private ViewModelTabItem _selectedTab;
    [ObservableProperty] private IDisposable? _activeContent;



    // public



    public ObservableCollection<ViewModelTabItem> Tabs => _tabs;



    // events



    // anevent



    // constructor



    public KeybindViewModel()
    {
        _isKeySelected = false;
        _key = null;
        _keybindModel = null;
        _configModel = null;

        _label = "null";
        _tabs = [
            new("On Pressed", () => new ActionGroupViewModel(_keybindModel!.OnPressed)),
            new("On Released", () => new ActionGroupViewModel(_keybindModel!.OnReleased)),
            new("On Repeated", () => new ActionGroupViewModel(_keybindModel!.OnRepeated)),
        ];
        _selectedTab = _tabs[0];
        _activeContent = null;
    }



    // model calls







    // model callbacks







    // other callbacks



    partial void OnSelectedTabChanged(ViewModelTabItem value) => SetContent(value.CreateTabViewModel());



    // setters



    public void SetKeySelected(DisplayKey key, KeybindConfigModel configModel, int slotID)
    {
        if (_key == key)
        {
            DeselectKeys(null);
            return;
        }

        _key = key;
        _keybindModel = configModel.Keybinds.GetValueOrDefault(key.VK) ?? new();
        _configModel = configModel;

        SetContent(SelectedTab.CreateTabViewModel());
        Label = $"Key: {key.Label.Replace('\n', '-')}   Config: {_configModel.Name}   Slot: {slotID}";
        IsKeySelected = true;
    }



    public void DeselectKeys(IReadOnlyList<DisplayKey>? keys)
    {
        IsKeySelected = false;
        _key = null;
        _keybindModel = null;
        _configModel = null;
        SetContent(null);
    }



    private void SetContent(IDisposable? content)
    {
        IDisposable? oldContent = ActiveContent;
        ActiveContent = content;
        oldContent?.Dispose();
    }



    // disposal



    public override void Dispose()
    {
        ActiveContent?.Dispose();
    }
}