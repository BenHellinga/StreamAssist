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

        bottomPanel.SetContent(null);

        return new(model, manager, bottomPanel);
    }



    // constructor



    public KeyboardComponentViewModel(KeyboardComponentModel model, KeyboardComponentManager manager, PanelViewModel keyConfiguratorPanel)
    {
        var keybindConfigurator = new KeybindViewModel();
        keyConfiguratorPanel.SetContent(keybindConfigurator);

        _keyboardList = new KeyboardListViewModel(model.ListModel, manager.GetInputManager(), keybindConfigurator);
    }



    // disposal



    public override void Dispose()
    {
        _keyboardList.Dispose();
    }
}