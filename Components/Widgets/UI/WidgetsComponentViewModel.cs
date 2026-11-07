using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Input.Model;
using StreamAssist.Components.Widgets.Model;
using StreamAssist.Core.Mvvm;
using StreamAssist.Shell;

namespace StreamAssist.Components.Widgets.UI;



// the Widgets tab: the config bar, the widget boxes, and the widget list past a splitter (the same width as the Input
// tab's input device list and the Dashboard's dock list). Nothing in the bottom panel: widgets have no bindings
public partial class WidgetsComponentViewModel : ViewModelBase
{
    // private



    private readonly WidgetListViewModel _widgetList;
    private readonly WindowLayoutModel _layout;
    private readonly WidgetPaletteViewModel _palette;
    [ObservableProperty] private GridLength _widgetListWidth; // bound two-way to the widget list column



    // public



    public WidgetListViewModel WidgetList => _widgetList;
    public WidgetPaletteViewModel Palette => _palette;



    // activator



    public static WidgetsComponentViewModel ActivateTab(WidgetsComponentModel model, InputListModel inputs, WindowLayoutModel layout, PanelViewModel sidePanel, PanelViewModel bottomPanel)
    {
        sidePanel.TransferContent(null);
        bottomPanel.TransferContent(null);

        return new(model, inputs, layout);
    }



    // constructor



    public WidgetsComponentViewModel(WidgetsComponentModel model, InputListModel? inputs, WindowLayoutModel layout)
    {
        _widgetList = new(model, inputs);
        _layout = layout;
        _palette = new();
        _widgetListWidth = new(_layout.GetPanelSize(WindowLayoutModel.SideListPanel, 250));

        _layout.PanelSizeChanged += OnLayoutPanelSizeChanged;
    }



    // ui events



    // the widget list's splitter was dragged
    partial void OnWidgetListWidthChanged(GridLength value)
    {
        if (value.IsAbsolute) _layout.SetPanelSize(WindowLayoutModel.SideListPanel, value.Value);
    }



    private void OnLayoutPanelSizeChanged(string panel, double size)
    {
        if (panel == WindowLayoutModel.SideListPanel) WidgetListWidth = new(size);
    }



    // disposal



    public override void Dispose()
    {
        _widgetList.Dispose();
        _palette.Dispose();
        _layout.PanelSizeChanged -= OnLayoutPanelSizeChanged;
    }
}
