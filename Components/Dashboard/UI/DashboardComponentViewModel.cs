using System.Linq;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Components.Dashboard.Model;
using StreamAssist.Core.Mvvm;
using StreamAssist.Shell;

namespace StreamAssist.Components.Dashboard.UI;



// the Dashboard tab: the config bar in the header, the docks' regions below it with the dock list (kinds to drag out)
// beside them, like the Chat tab's block list (the side panel is left empty), and the binding of the selected dock
// button in the bottom panel's editor. The regions are made again from the model whenever the layout's shape changes (a dock
// added, moved or removed, another config picked); dragging a splitter only saves sizes, so nothing is rebuilt
public partial class DashboardComponentViewModel : ViewModelBase
{
    // private



    private readonly DashboardComponentModel _model;
    private readonly ConfigBarViewModel _configBar;
    private readonly WindowLayoutModel _layout;
    private readonly DockPaletteViewModel _dockPalette;
    private readonly BindableEditorViewModel _editor; // lives in the bottom panel, which disposes it
    private DashboardConfigModel? _config; // the one the regions were made from
    private DockButton? _selected; // the button open in the editor

    [ObservableProperty] private ViewModelBase _regions;
    [ObservableProperty] private GridLength _dockListWidth; // bound two-way to the dock list column



    // public



    public ConfigBarViewModel ConfigBar => _configBar;
    public DockPaletteViewModel DockPalette => _dockPalette;



    // activator



    public static DashboardComponentViewModel ActivateTab(DashboardComponentModel model, WindowLayoutModel layout, PanelViewModel sidePanel, PanelViewModel bottomPanel)
    {
        var editor = new BindableEditorViewModel(layout, "No button selected");
        sidePanel.TransferContent(null);
        bottomPanel.TransferContent(editor);
        return new(model, layout, editor);
    }



    // constructor



    public DashboardComponentViewModel(DashboardComponentModel model, WindowLayoutModel layout, BindableEditorViewModel editor)
    {
        _model = model;
        _layout = layout;
        _editor = editor;
        _configBar = new(model.Configs);
        _dockPalette = new(model);
        _dockListWidth = new(_layout.GetPanelSize(WindowLayoutModel.SideListPanel, 250));
        _config = null;
        _selected = null;
        _regions = new NoConfigViewModel();

        DockButtonSelection.Set(null); // a fresh tab starts with nothing open
        ShowConfig();

        _model.Configs.ListChanged += OnConfigsChanged;
        DockButtonSelection.Changed += OnSelectionChanged;
        _layout.PanelSizeChanged += OnLayoutPanelSizeChanged;
    }



    // drag drop: a card dropped on the empty dashboard (a dock takes its own drops)



    public bool CanAcceptNewDock(object? payload) => payload is NewDockPayload && _config is { Layout: null };



    public void DropNewDock(object? payload)
    {
        if (CanAcceptNewDock(payload)) _config!.TryAddDock(((NewDockPayload)payload!).Kind);
    }



    // ui events



    // the dock list's splitter was dragged
    partial void OnDockListWidthChanged(GridLength value)
    {
        if (value.IsAbsolute) _layout.SetPanelSize(WindowLayoutModel.SideListPanel, value.Value);
    }



    private void OnLayoutPanelSizeChanged(string panel, double size)
    {
        if (panel == WindowLayoutModel.SideListPanel) DockListWidth = new(size);
    }



    // a button's bind box was clicked (or the selection was cleared)
    private void OnSelectionChanged()
    {
        if (_selected != null)
        {
            _selected.BindableChanged -= OnSelectedBindableChanged;
            _selected.NameChanged -= OnSelectedNameChanged;
        }

        _editor.ClearBindable();
        _selected = DockButtonSelection.Selected;
        if (_selected == null) return;

        _selected.BindableChanged += OnSelectedBindableChanged;
        _selected.NameChanged += OnSelectedNameChanged;
        ShowSelected();
    }



    // model events



    private void OnConfigsChanged()
    {
        if (_model.Config != _config) ShowConfig();
    }



    // something in the config changed: close the editor if its button is gone (deleted, or its dock removed)
    private void OnConfigChanged()
    {
        if (_selected != null && _config?.Docks.Any(d => d.Buttons.Rows.Contains(_selected)) != true)
            DockButtonSelection.Set(null);
    }



    // bound or unbound from somewhere else (a binding dropped on it, its blocks all removed); our own draft being
    // saved is the editor's binding already, so it keeps editing it
    private void OnSelectedBindableChanged()
    {
        if (_selected == null || _selected.Bindable == _editor.Bindable) return;
        ShowSelected();
    }



    private void OnSelectedNameChanged()
    {
        if (_selected != null) _editor.Label = Label(_selected);
    }



    // helpers



    // follows the config in use (its layout changes), and shows its regions, with no button open
    private void ShowConfig()
    {
        _config?.LayoutChanged -= BuildRegions;
        _config?.Changed -= OnConfigChanged;
        _config = _model.Config;
        _config?.LayoutChanged += BuildRegions;
        _config?.Changed += OnConfigChanged;

        DockButtonSelection.Set(null);
        BuildRegions();
    }



    // the button's binding, or a fresh empty draft (bound to it on its first change) if it has none
    private void ShowSelected()
    {
        if (_selected == null) return;

        if (_selected.Bindable != null) _editor.ShowBindable(_selected.Bindable, Label(_selected));
        else _editor.ShowDraft(Label(_selected), _selected.TryBind, startWithOnPressed: false); // a press just fires once
    }



    private static string Label(DockButton button) => $"Button: {button.Name}";



    private void BuildRegions()
    {
        var old = Regions;
        Regions = _config == null ? new NoConfigViewModel()
            : _config.Layout == null ? new NoConfigViewModel("Drag a dock here from the list on the right")
            : Build(_config, _config.Layout);
        old.Dispose();
    }



    private ViewModelBase Build(DashboardConfigModel config, DockLayoutNode node)
    {
        if (node.IsDock) return new DockViewModel(_model, config, config.Docks.First(d => d.Id == node.DockId));
        return new DockSplitViewModel(config, node, node.Children.Select(c => Build(config, c)).ToList());
    }



    // disposal



    public override void Dispose()
    {
        _model.Configs.ListChanged -= OnConfigsChanged;
        DockButtonSelection.Changed -= OnSelectionChanged;
        _layout.PanelSizeChanged -= OnLayoutPanelSizeChanged;
        _config?.LayoutChanged -= BuildRegions;
        _config?.Changed -= OnConfigChanged;

        DockButtonSelection.Set(null); // runs OnSelectionChanged no more, so let go of the button here
        if (_selected != null)
        {
            _selected.BindableChanged -= OnSelectedBindableChanged;
            _selected.NameChanged -= OnSelectedNameChanged;
            _selected = null;
        }
        _editor.ClearBindable();

        Regions.Dispose();
        _configBar.Dispose();
        _dockPalette.Dispose();
    }
}
