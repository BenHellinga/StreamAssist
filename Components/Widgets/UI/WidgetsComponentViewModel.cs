using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Components.Input.Model;
using StreamAssist.Components.Widgets.Model;
using StreamAssist.Core.Mvvm;
using StreamAssist.Shell;

namespace StreamAssist.Components.Widgets.UI;



// the Widgets tab: the config bar, the widget boxes, and the widget list past a splitter (the same width as the Input
// tab's input device list and the Dashboard's dock list). The bottom panel edits the widget binding whose bind box was
// clicked (an image party's When Corner Hit)
public partial class WidgetsComponentViewModel : ViewModelBase
{
    // private



    private readonly WidgetListViewModel _widgetList;
    private readonly WindowLayoutModel _layout;
    private readonly WidgetPaletteViewModel _palette;
    private readonly BindableEditorViewModel? _editor; // lives in the bottom panel, which disposes it
    private ImagePartyWidget? _selected; // the one whose binding is open
    [ObservableProperty] private GridLength _widgetListWidth; // bound two-way to the widget list column



    // public



    public WidgetListViewModel WidgetList => _widgetList;
    public WidgetPaletteViewModel Palette => _palette;



    // activator



    public static WidgetsComponentViewModel ActivateTab(WidgetsComponentModel model, InputListModel inputs, WindowLayoutModel layout, PanelViewModel sidePanel, PanelViewModel bottomPanel)
    {
        var editor = new BindableEditorViewModel(layout, "No binding selected");
        sidePanel.TransferContent(null);
        bottomPanel.TransferContent(editor);

        return new(model, inputs, layout, editor);
    }



    // constructor



    public WidgetsComponentViewModel(WidgetsComponentModel model, InputListModel? inputs, WindowLayoutModel layout, BindableEditorViewModel? editor = null)
    {
        _widgetList = new(model, inputs);
        _layout = layout;
        _editor = editor;
        _palette = new();
        _widgetListWidth = new(_layout.GetPanelSize(WindowLayoutModel.SideListPanel, 250));

        _layout.PanelSizeChanged += OnLayoutPanelSizeChanged;
        WidgetBindingSelection.Set(null); // a fresh tab starts with nothing open
        WidgetBindingSelection.Changed += OnSelectionChanged;
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



    // a widget binding's bind box was clicked: its binding (or a fresh draft, bound on its first change) in the editor



    private void OnSelectionChanged()
    {
        if (_selected != null) _selected.CornerBindableChanged -= OnSelectedBindableChanged;
        _editor?.ClearBindable();
        _selected = WidgetBindingSelection.Selected;
        if (_selected == null) return;

        _selected.CornerBindableChanged += OnSelectedBindableChanged;
        ShowSelected();
    }



    // bound or unbound from somewhere else; our own draft being saved is the editor's binding already
    private void OnSelectedBindableChanged()
    {
        if (_selected == null || _selected.CornerBindable == _editor?.Bindable) return;
        ShowSelected();
    }



    private void ShowSelected()
    {
        if (_selected == null || _editor == null) return;

        var label = $"{_selected.Name}: When Corner Hit";
        if (_selected.CornerBindable != null) _editor.ShowBindable(_selected.CornerBindable, label);
        else _editor.ShowDraft(label, _selected.TryBindCorner, startWithOnPressed: false);
    }



    // disposal



    public override void Dispose()
    {
        _widgetList.Dispose();
        _palette.Dispose();
        _layout.PanelSizeChanged -= OnLayoutPanelSizeChanged;
        WidgetBindingSelection.Changed -= OnSelectionChanged;
        if (_selected != null) _selected.CornerBindableChanged -= OnSelectedBindableChanged;
        WidgetBindingSelection.Set(null);
        _editor?.ClearBindable();
    }
}
