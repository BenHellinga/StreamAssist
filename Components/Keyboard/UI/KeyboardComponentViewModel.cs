using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
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
    private readonly WindowLayoutModel _layout;
    private readonly InputDevicePaletteViewModel _palette;
    [ObservableProperty] private GridLength _inputDeviceListWidth; // bound two-way to the input device list column



    // public



    public KeyboardListViewModel KeyboardList => _keyboardList;
    public InputDevicePaletteViewModel Palette => _palette;



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