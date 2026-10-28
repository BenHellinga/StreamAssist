using StreamAssist.Core.Mvvm;
using StreamAssist.Components.Keyboard.Background;
using StreamAssist.Components.Keyboard.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Keyboard.UI;



// view model to modify a KeyboardComponentModel object
public partial class KeyboardComponentViewModel : ViewModelBase
{
    // private



    private KeyboardListViewModel _keyboardList;



    // public



    public KeyboardListViewModel KeyboardList => _keyboardList;



    // activator



    public static KeyboardComponentViewModel ActivateTab(KeyboardComponentModel model, KeyboardComponentManager manager, WindowLayoutModel layout, PanelViewModel sidePanel, PanelViewModel bottomPanel)
    {
        // no side panel, the block list lives in the bottom panel next to the editor
        sidePanel.TransferContent(null);
        bottomPanel.TransferContent(null);

        return new(model, manager, layout, bottomPanel);
    }



    // constructor



    public KeyboardComponentViewModel(KeyboardComponentModel model, KeyboardComponentManager manager, WindowLayoutModel layout, PanelViewModel keyConfiguratorPanel)
    {
        var keybindConfigurator = new KeybindViewModel(layout);
        keyConfiguratorPanel.TransferContent(keybindConfigurator);

        _keyboardList = new KeyboardListViewModel(model.ListModel, manager.GetInputManager(), keybindConfigurator);
    }



    // disposal



    public override void Dispose()
    {
        _keyboardList.Dispose();
    }
}