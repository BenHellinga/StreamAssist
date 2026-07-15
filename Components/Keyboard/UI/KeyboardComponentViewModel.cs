using StreamAssist.Components.Core;
using StreamAssist.Components.AppAction.UI;
using StreamAssist.Components.Keyboard.Background;
using StreamAssist.Components.Keyboard.Model;
using StreamAssist.Components.Main.UI;

namespace StreamAssist.Components.Keyboard.UI;



public partial class KeyboardComponentViewModel : ViewModelBase
{
    // private



    private KeyboardListViewModel _keyboardList;



    // public



    public KeyboardListViewModel KeyboardList => _keyboardList;



    // activator



    public static KeyboardComponentViewModel ActivateTab(KeyboardComponentModel model, KeyboardComponentManager manager, PanelViewModel sidePanel, PanelViewModel bottomPanel)
    {
        if (sidePanel.Content is not ActionListViewModel)
            sidePanel.SetContent(new ActionListViewModel());

        return new(model, manager, bottomPanel);
    }



    // constructor



    public KeyboardComponentViewModel(KeyboardComponentModel model, KeyboardComponentManager manager, PanelViewModel keyConfiguratorPanel)
    {
        var keybindViewModel = new KeybindViewModel();
        keyConfiguratorPanel.SetContent(keybindViewModel);

        _keyboardList = new KeyboardListViewModel(model.ListModel, manager.GetInputManager());
        _keyboardList.KeySelected += keybindViewModel.SetKeySelected;
        _keyboardList.KeysDeselected += keybindViewModel.DeselectKeys;
    }



    // disposal



    public override void Dispose()
    {
        _keyboardList.Dispose();
    }
}