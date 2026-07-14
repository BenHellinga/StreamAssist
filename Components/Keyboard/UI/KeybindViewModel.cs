using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.AppAction.UI;
using StreamAssist.Components.Core;
using StreamAssist.Components.Keyboard.Model;
using StreamAssist.Components.Main.UI;

namespace StreamAssist.Components.Keyboard.UI;



public partial class KeybindViewModel : ViewModelBase
{
    // private storage fields
    private KeybindModel? _keybindModel;
    [ObservableProperty] private ViewModelTabItem _selectedTab;
    [ObservableProperty] private IDisposable? _activeContent;
    [ObservableProperty] private bool _isKeySelected;
    private ObservableCollection<ViewModelTabItem> _tabs;

    // public getter properties
    public ObservableCollection<ViewModelTabItem> Tabs => _tabs;



    // constructor



    public KeybindViewModel()
    {
        _keybindModel = null;

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



    // setter



    public void SetBindingModel(KeybindModel? keybindModel)
    {
        if (keybindModel == null)
        {
            IsKeySelected = false;
            _keybindModel = null;
            ActiveContent?.Dispose();
        }
        else
        {
            _keybindModel = keybindModel;
            ActiveContent = SelectedTab.CreateTabViewModel();
            IsKeySelected = true;
        }
    }



    // disposal



    public override void Dispose()
    {
        ActiveContent?.Dispose();
    }
}