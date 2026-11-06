using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Core.Mvvm;
using StreamAssist.Components.Input.Background;
using StreamAssist.Components.Input.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Input.UI;



// view model to modify a InputComponentModel object
public partial class InputComponentViewModel : ViewModelBase
{
    // private



    private InputListViewModel _keyboardList;
    private readonly WindowLayoutModel _layout;
    private readonly InputDevicePaletteViewModel _palette;
    [ObservableProperty] private GridLength _inputDeviceListWidth; // bound two-way to the input device list column



    // public



    public InputListViewModel KeyboardList => _keyboardList;
    public InputDevicePaletteViewModel Palette => _palette;



    // activator



    public static InputComponentViewModel ActivateTab(InputComponentModel model, InputComponentManager manager, WindowLayoutModel layout, PanelViewModel sidePanel, PanelViewModel bottomPanel)
    {
        // no side panel, the block list lives in the bottom panel next to the editor
        sidePanel.TransferContent(null);
        bottomPanel.TransferContent(null);

        return new(model, manager, layout, bottomPanel);
    }



    // constructor



    public InputComponentViewModel(InputComponentModel model, InputComponentManager manager, WindowLayoutModel layout, PanelViewModel keyConfiguratorPanel)
    {
        var keybindConfigurator = new KeybindViewModel(layout);
        keyConfiguratorPanel.TransferContent(keybindConfigurator);

        _keyboardList = new InputListViewModel(model.ListModel, manager.GetInputManager(), keybindConfigurator);
        _layout = layout;
        _palette = new();
        _inputDeviceListWidth = new(_layout.GetPanelSize(WindowLayoutModel.SideListPanel, 250));

        _layout.PanelSizeChanged += OnLayoutPanelSizeChanged;
    }



    // ui events



    // the input device list's splitter was dragged
    partial void OnInputDeviceListWidthChanged(GridLength value)
    {
        if (value.IsAbsolute) _layout.SetPanelSize(WindowLayoutModel.SideListPanel, value.Value);
    }



    private void OnLayoutPanelSizeChanged(string panel, double size)
    {
        if (panel == WindowLayoutModel.SideListPanel) InputDeviceListWidth = new(size);
    }



    // disposal



    public override void Dispose()
    {
        _keyboardList.Dispose();
        _palette.Dispose();
        _layout.PanelSizeChanged -= OnLayoutPanelSizeChanged;
    }
}