using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Data.Model;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Data.UI;



// one cell: its text box holds what's typed, leaving it (Enter or clicking away, see CommitOnLeave) asks the model to set
// it; the cell then shows what the model has (a refused value goes back, the new row's cells empty again)
public partial class CellViewModel : ObservableObject, ICommitOnLeave
{
    private readonly Func<string, bool> _commit;
    private string _shown; // what the model has, as text

    [ObservableProperty] private string _text;

    public CellViewModel(string text, Func<string, bool> commit, string placeholder = "")
    {
        _text = text;
        _shown = text;
        _commit = commit;
        Placeholder = placeholder;
    }

    public string Placeholder { get; }

    public void Commit()
    {
        if (Text == _shown) return;
        _commit(Text); // ends with Show (what the model has now)
    }

    // what the model has now (doesn't ask the model to set anything)
    public void Show(string text)
    {
        _shown = text;
        Text = text;
    }
}



// one row: its id, a cell per column, and a button to delete it
public partial class RowViewModel(int id, IReadOnlyList<CellViewModel> cells, Action<int> delete) : ObservableObject
{
    public int Id { get; } = id;
    public IReadOnlyList<CellViewModel> Cells { get; } = cells;

    [RelayCommand] private void OnDeletePressed() => delete(Id);
}



// one column's header: on custom tables its name and type can be changed. Clearing the name removes the column if it's
// empty (it's put back if it has values); in the grid's delete mode a − on the header deletes it, values and all
public partial class ColumnHeaderViewModel : ObservableObject, ICommitOnLeave
{
    private readonly DataModel _model;
    private readonly TableModel _table;
    private readonly TableColumn _column;
    private readonly Action<string> _sort;

    [ObservableProperty] private string _nameInput;
    [ObservableProperty] private string _kindName;
    [ObservableProperty] private string _sortText; // ↕ not sorted by it, ▲ least to greatest, ▼ greatest to least
    [ObservableProperty] private bool _isDeleteMode; // the grid's Delete columns is on

    public ColumnHeaderViewModel(DataModel model, TableModel table, TableColumn column, Action<string> sort, bool isDeleteMode)
    {
        _model = model;
        _table = table;
        _column = column;
        _sort = sort;
        _sortText = TableGridViewModel.NotSorted;
        _nameInput = column.Name;
        _kindName = column.Kind.ToString();
        _isDeleteMode = isDeleteMode;
    }

    public string Name => _column.Name;
    public bool IsEditable => !_table.IsBuiltIn;
    public IReadOnlyList<string> Kinds => DataModel.ColumnKinds.Select(k => k.ToString()).ToList();

    // the name box was left (CommitOnLeave): rename, or remove an empty column whose name was cleared; refused, it goes back
    public void Commit()
    {
        if (NameInput == _column.Name) return;

        var done = NameInput.Trim() == ""
            ? _model.TryDeleteColumn(_table.Name, _column.Name, onlyIfEmpty: true)
            : _model.TryRenameColumn(_table.Name, _column.Name, NameInput);
        if (!done) NameInput = _column.Name;
    }

    // the dropdown sets this while it's writing it, so a refused type is put back afterwards (it'd ignore it now)
    partial void OnKindNameChanged(string value)
    {
        if (value == _column.Kind.ToString()) return;
        if (Enum.TryParse<ValueKind>(value, out var kind) && _model.TrySetColumnKind(_table.Name, _column.Name, kind)) return;

        Dispatcher.UIThread.Post(() => KindName = _column.Kind.ToString());
    }

    [RelayCommand] private void OnSortPressed() => _sort(_column.Name);

    // only shown in delete mode, which is the safety: it deletes the column and everything in it straight away
    [RelayCommand] private void OnDeletePressed() => _model.TryDeleteColumn(_table.Name, _column.Name);
}



// the open table in the Data tab: a header row, a row per table row (its id, then its cells) and, for custom tables, the
// controls to rename / delete it and add columns. Follows the model's events, so changes from blocks show up live.
// Rows can be searched (any cell or the id contains the text) and sorted by a column (pressing its header's button goes
// least to greatest, greatest to least, then back to id order), and filtered by a query (see RowQuery, applied by Run
// query, both have to match). A changed value doesn't re-sort straight away, so a row
// doesn't jump away while it's being edited (searching or sorting again puts everything in order)
public partial class TableGridViewModel : ViewModelBase, ICommitOnLeave
{
    // private



    public const string NotSorted = "↕";

    private readonly DataModel _model;
    private readonly TableModel _table;
    private readonly Dictionary<int, RowViewModel> _rowViews; // every row's view (shown or filtered out), by id
    private string? _sortColumn; // null = by id
    private bool _sortDescending;

    [ObservableProperty] private string _searchText;
    [ObservableProperty] private string _queryText; // what's typed in the query box (applied by Run query)
    private RowQuery _query; // the query that was run, every row passes until one is
    [ObservableProperty] private string _idSortText;
    [ObservableProperty] private string _tableNameInput;
    [ObservableProperty] private string _newColumnName; // the empty box after the last column, a name typed there makes one
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(DeleteTableText))] private bool _isDeleteArmed;
    [ObservableProperty] private bool _isDeleteColumnsMode; // the Delete columns button is on (shown green)
    [ObservableProperty] private IReadOnlyList<CellViewModel> _newRowCells; // the empty row at the bottom, a value typed in it makes a row
    [ObservableProperty] private IReadOnlyList<ColumnHeaderViewModel> _columns;



    // public



    public TableModel Table => _table;
    public string TableName => _table.Name;
    public bool IsCustom => !_table.IsBuiltIn;
    public ObservableCollection<RowViewModel> Rows { get; }
    public IReadOnlyList<string> Kinds => DataModel.ColumnKinds.Select(k => k.ToString()).ToList();
    public string DeleteTableText => IsDeleteArmed ? "Sure? Click again" : "Delete table";



    // constructor



    public TableGridViewModel(DataModel model, TableModel table)
    {
        _model = model;
        _table = table;
        _tableNameInput = table.Name;
        _newColumnName = "";
        _isDeleteArmed = false;
        _isDeleteColumnsMode = false;
        _newRowCells = [];
        _columns = [];
        _searchText = "";
        _queryText = "";
        _query = RowQuery.All;
        _idSortText = NotSorted;
        _rowViews = [];
        _sortColumn = null;
        _sortDescending = false;

        Rows = [];

        SubscribeToModel();
        Rebuild();
    }



    // ui events



    partial void OnSearchTextChanged(string value) => ShowRows();



    // applies the query box (an empty one shows every row again). One that can't be read is logged and the rows stay as
    // they were
    [RelayCommand] private void OnRunQueryPressed()
    {
        var query = RowQuery.TryParse(QueryText, _table, out var error);
        if (query == null)
        {
            AppLog.WriteError($"can't run query ({error})");
            return;
        }

        _query = query;
        ShowRows();
    }



    [RelayCommand] private void OnSortByIdPressed() => Sort(null);



    // a name typed in the box after the last column (saved on Enter or clicking away) makes a Text column, the box empties
    // the new column box was left (CommitOnLeave, it's the grid's only box that commits): a Text column with its name,
    // and the box empties for the next one
    public void Commit()
    {
        if (NewColumnName.Trim() == "") return;
        if (_model.TryAddColumn(_table.Name, NewColumnName, ValueKind.Text)) NewColumnName = "";
    }



    // shows (or hides) a − on every column's header, which deletes it straight away
    [RelayCommand] private void OnDeleteColumnsPressed()
    {
        IsDeleteColumnsMode = !IsDeleteColumnsMode;
        foreach (var header in Columns) header.IsDeleteMode = IsDeleteColumnsMode;
    }



    [RelayCommand] private void OnRenumberPressed() => _model.TryRenumberRows(_table.Name);



    [RelayCommand] private void OnRenamePressed()
    {
        if (!_model.TryRenameTable(_table.Name, TableNameInput)) TableNameInput = _table.Name;
    }



    // the first press arms it, the second deletes the table (the tab closes the grid when the model says it's gone)
    [RelayCommand] private void OnDeleteTablePressed()
    {
        if (!IsDeleteArmed) IsDeleteArmed = true;
        else _model.TryDeleteTable(_table.Name);
    }



    // model events



    private void OnColumnsChanged(TableModel table, TableColumn column)
    {
        if (table == _table) Rebuild();
    }



    private void OnColumnRenamed(TableModel table, TableColumn column, string oldName)
    {
        if (table == _table) Rebuild();
    }



    private void OnTableRenamed(TableModel table, string oldName)
    {
        if (table != _table) return;
        TableNameInput = table.Name;
        OnPropertyChanged(nameof(TableName));
    }



    // a new row goes at the end (even if the search or sort would put it elsewhere), so it's where it was just added
    private void OnRowAdded(TableModel table, TableRow row)
    {
        if (table != _table) return;
        var view = CreateRow(row);
        _rowViews[row.Id] = view;
        Rows.Add(view);
    }



    private void OnRowsRenumbered(TableModel table, IReadOnlyList<(int Old, int New)> _)
    {
        if (table == _table) Rebuild();
    }



    private void OnRowRemoved(TableModel table, TableRow row)
    {
        if (table != _table || !_rowViews.Remove(row.Id, out var shown)) return;
        Rows.Remove(shown);
    }



    private void OnValueChanged(TableModel table, TableRow row, TableColumn column)
    {
        if (table != _table) return;
        var index = _table.ColumnList.IndexOf(column);
        if (_rowViews.TryGetValue(row.Id, out var shown) && index >= 0 && index < shown.Cells.Count) shown.Cells[index].Show(Text(row.Get(column.Name)));
    }



    // helpers



    private void Rebuild()
    {
        if (_sortColumn != null && _table.FindColumn(_sortColumn) == null) _sortColumn = null; // its column is gone
        Columns = _table.Columns.Select(c => new ColumnHeaderViewModel(_model, _table, c, Sort, IsDeleteColumnsMode)).ToList();

        _rowViews.Clear();
        foreach (var row in _table.Rows) _rowViews[row.Id] = CreateRow(row);
        NewRowCells = _table.Columns.Select((column, i) =>
        {
            CellViewModel? cell = null;
            cell = new CellViewModel("", text => AddRowWith(column.Name, text, cell!), i == 0 ? "New row…" : "");
            return cell;
        }).ToList();
        ShowRows();
    }



    // a value typed in the empty row: a new row with it (the row appears through RowAdded), the empty row empties again.
    // A value its column refuses leaves no row behind
    private bool AddRowWith(string column, string text, CellViewModel cell)
    {
        cell.Show("");
        if (text.Trim() == "" || !_model.TryAddRow(_table.Name, out var row)) return false;

        if (_model.TrySetValue(_table.Name, row.Id, column, text)) return true;

        _model.TryDeleteRow(_table.Name, row.Id);
        return false;
    }



    // pressing a column's sort button again goes ▲ → ▼ → back to id order, another column starts at ▲
    private void Sort(string? column)
    {
        var same = string.Equals(column, _sortColumn, StringComparison.OrdinalIgnoreCase);
        if (!same) (_sortColumn, _sortDescending) = (column, false);
        else if (!_sortDescending) _sortDescending = true;
        else (_sortColumn, _sortDescending) = (null, column == null && !_sortDescending);

        ShowRows();
    }



    // the rows matching the search, in the sort order, and the headers' sort buttons to match
    private void ShowRows()
    {
        var search = SearchText.Trim();
        var matching = _table.Rows.Where(r => _query.Matches(_table, r) && (search == "" || r.Id.ToString() == search
            || _table.Columns.Any(c => Text(r.Get(c.Name)).Contains(search, StringComparison.OrdinalIgnoreCase))));

        var column = _sortColumn == null ? null : _table.FindColumn(_sortColumn);
        IEnumerable<TableRow> ordered = column == null
            ? (_sortDescending ? matching.OrderByDescending(r => r.Id) : matching.OrderBy(r => r.Id))
            : matching.OrderBy(r => r.Get(column.Name) == null) // empty cells last, either way
                .ThenBy(r => r.Get(column.Name), _sortDescending ? new ReverseComparer(ValueComparer) : ValueComparer)
                .ThenBy(r => r.Id);

        Rows.Clear();
        foreach (var row in ordered)
            if (_rowViews.TryGetValue(row.Id, out var view)) Rows.Add(view);

        string Glyph(bool sorted) => !sorted ? NotSorted : _sortDescending ? "▼" : "▲";
        IdSortText = Glyph(column == null && (_sortColumn == null && _sortDescending));
        foreach (var header in Columns) header.SortText = Glyph(column != null && string.Equals(header.Name, column.Name, StringComparison.OrdinalIgnoreCase));
    }



    // numbers as numbers, true after false, text ignoring case
    private static readonly IComparer<object?> ValueComparer = Comparer<object?>.Create((a, b) => (a, b) switch
    {
        (int x, int y) => x.CompareTo(y),
        (double or int, double or int) => Convert.ToDouble(a).CompareTo(Convert.ToDouble(b)),
        (bool x, bool y) => x.CompareTo(y),
        _ => string.Compare(Text(a), Text(b), StringComparison.OrdinalIgnoreCase),
    });



    private sealed class ReverseComparer(IComparer<object?> inner) : IComparer<object?>
    {
        public int Compare(object? a, object? b) => inner.Compare(b, a);
    }



    private RowViewModel CreateRow(TableRow row)
    {
        var cells = _table.Columns.Select(column =>
        {
            CellViewModel? cell = null;
            cell = new CellViewModel(Text(row.Get(column.Name)), text =>
            {
                var ok = _model.TrySetValue(_table.Name, row.Id, column.Name, text);
                cell!.Show(Text(row.Get(column.Name))); // refused: back to what's there; accepted: how the model keeps it (e.g. 5.0 → 5)
                if (ok && text.Trim() == "") _model.TryDeleteRow(_table.Name, row.Id, onlyIfEmpty: true); // its last value was cleared
                return ok;
            });
            return cell;
        }).ToList();

        return new RowViewModel(row.Id, cells, id => _model.TryDeleteRow(_table.Name, id));
    }



    private static string Text(object? value) => value == null ? "" : ValueTypes.ToText(value);



    // subscriptions



    private void SubscribeToModel()
    {
        _model.ColumnAdded += OnColumnsChanged;
        _model.ColumnRemoved += OnColumnsChanged;
        _model.ColumnKindChanged += OnColumnsChanged;
        _model.ColumnRenamed += OnColumnRenamed;
        _model.TableRenamed += OnTableRenamed;
        _model.RowAdded += OnRowAdded;
        _model.RowsRenumbered += OnRowsRenumbered;
        _model.RowRemoved += OnRowRemoved;
        _model.ValueChanged += OnValueChanged;
    }



    private void UnsubscribeFromModel()
    {
        _model.ColumnAdded -= OnColumnsChanged;
        _model.ColumnRemoved -= OnColumnsChanged;
        _model.ColumnKindChanged -= OnColumnsChanged;
        _model.ColumnRenamed -= OnColumnRenamed;
        _model.TableRenamed -= OnTableRenamed;
        _model.RowAdded -= OnRowAdded;
        _model.RowsRenumbered -= OnRowsRenumbered;
        _model.RowRemoved -= OnRowRemoved;
        _model.ValueChanged -= OnValueChanged;
    }



    // disposal



    public override void Dispose() => UnsubscribeFromModel();
}
