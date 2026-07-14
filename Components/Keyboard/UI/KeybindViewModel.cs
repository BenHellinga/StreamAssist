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
    // private storage fields
    private KeyboardListModel? _listModel;
    private KeyboardSlotModel? _slotModel;
    private KeybindModel? _keybindModel;
    private DisplayKey? _key;
    [ObservableProperty] private string _label;
    [ObservableProperty] private ViewModelTabItem _selectedTab;
    [ObservableProperty] private IDisposable? _activeContent;
    [ObservableProperty] private bool _isKeySelected;
    private ObservableCollection<ViewModelTabItem> _tabs;

    // public getter properties
    public ObservableCollection<ViewModelTabItem> Tabs => _tabs;



    // constructor



    public KeybindViewModel()
    {
        _listModel = null;
        _slotModel = null;
        _keybindModel = null;
        _key = null;
        _label = "null";

        _tabs = [
            new("On Pressed", () => new ActionGroupViewModel("On Pressed", "Runs all actions when the key is pressed.", _keybindModel!.OnPressed)),
            new("On Released", () => new ActionGroupViewModel("On Released", "Runs all actions when the key is released.", _keybindModel!.OnReleased)),
            new("On Repeated", () => new ActionGroupViewModel("On Repeated", "Runs all actions when the key repeats while held.", _keybindModel!.OnRepeated)),
        ];
        _selectedTab = _tabs[0];
        _activeContent = null;
    }



    // callbacks



    partial void OnSelectedTabChanged(ViewModelTabItem value)
    {
        IDisposable? oldContent = ActiveContent;
        ActiveContent = value.CreateTabViewModel();
        oldContent?.Dispose();
    }



    // setters



    public void SetSelectedKey(DisplayKey key, KeyboardSlotModel slotModel, KeyboardListModel listModel)
    {
        if (_key == key)
        {
            DeselectKey();
            return;
        }

        _key = key;
        _slotModel = slotModel;
        _listModel = listModel;
        _keybindModel = slotModel.SelectedConfig!.Keybinds.GetValueOrDefault(key.VK) ?? new();

        ActiveContent = SelectedTab.CreateTabViewModel();
        Label = $"{key.VK} - slot {_slotModel.SlotID}";
        IsKeySelected = true;
    }



    public void DeselectKey()
    {
        IsKeySelected = false;
        _key = null;
        _keybindModel = null;
        ActiveContent?.Dispose();
    }



    // disposal



    public override void Dispose()
    {
        ActiveContent?.Dispose();
    }
}