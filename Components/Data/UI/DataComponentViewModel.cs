using StreamAssist.Components.Data.Model;
using StreamAssist.Core.Mvvm;
using StreamAssist.Shell;

namespace StreamAssist.Components.Data.UI;



// the Data tab: the table list in the window's side panel (search, Built-in / Custom), the picked table's grid in the
// middle, no bottom panel. Everything changes through DataModel, the grid and list follow its events
public partial class DataComponentViewModel : ViewModelBase
{
    // private



    private static string? _lastTable; // reopening the tab shows the table that was open (this session)

    private readonly DataModel _model;
    private TableGridViewModel? _grid;



    // public



    public TableGridViewModel? Grid
    {
        get => _grid;
        private set => SetProperty(ref _grid, value);
    }

    public bool HasGrid => _grid != null;



    // activator



    public static DataComponentViewModel ActivateTab(DataModel model, PanelViewModel sidePanel, PanelViewModel bottomPanel)
    {
        bottomPanel.TransferContent(null);

        var tab = new DataComponentViewModel(model);
        sidePanel.TransferContent(new TableListViewModel(model, tab.ShowTable, () => tab.Grid?.TableName));
        return tab;
    }



    // constructor



    public DataComponentViewModel(DataModel model)
    {
        _model = model;
        _grid = null;

        _model.TableRemoved += OnTableRemoved;

        ShowTable(_lastTable ?? DataModel.VariablesTable);
    }



    // public api



    public void ShowTable(string name)
    {
        var table = _model.FindTable(name);
        if (table == null) return;

        _lastTable = table.Name;
        var old = _grid;
        Grid = new TableGridViewModel(_model, table);
        old?.Dispose();
        OnPropertyChanged(nameof(HasGrid));
    }



    // model events



    private void OnTableRemoved(TableModel table)
    {
        if (_grid?.Table != table) return;

        var old = _grid;
        Grid = null;
        old.Dispose();
        OnPropertyChanged(nameof(HasGrid));
    }



    // disposal



    public override void Dispose()
    {
        _model.TableRemoved -= OnTableRemoved;
        _grid?.Dispose();
    }
}
