using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Data.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Data.UI;



// one table in the list, clicking it opens it
public partial class TableItemViewModel(string name, Action<string> open) : ObservableObject
{
    [ObservableProperty] private bool _isOpen;

    public string Name { get; } = name;

    [RelayCommand] private void OnPressed() => open(Name);
}



// one heading (Built-in or Custom) and its tables, clicking the heading collapses it (this session)
public partial class TableGroupViewModel(string title) : ObservableObject
{
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Heading), nameof(Arrow))] private bool _isCollapsed;
    [ObservableProperty] private IReadOnlyList<TableItemViewModel> _tables = [];

    public string Title { get; } = title;
    public string HeadingText => Title;
    public string Arrow => IsCollapsed ? "▸" : "▾";
    public string Heading => $"{HeadingText} {Arrow}";

    [RelayCommand] private void OnHeadingPressed() => IsCollapsed = !IsCollapsed;
}



// the side panel of the Data tab: a search box, Built-in and Custom tables, and a box to make a new (custom) table
public partial class TableListViewModel : ViewModelBase, ICommitOnLeave
{
    // private



    private readonly DataModel _model;
    private readonly Action<string> _open;
    private readonly Func<string?> _openTable;

    [ObservableProperty] private string _searchText;
    [ObservableProperty] private string _newTableName;



    // public



    public TableGroupViewModel BuiltIn { get; }
    public TableGroupViewModel Custom { get; }



    // constructor



    public TableListViewModel(DataModel model, Action<string> open, Func<string?> openTable)
    {
        _model = model;
        _open = open;
        _openTable = openTable;
        _searchText = "";
        _newTableName = "";

        BuiltIn = new("Built-in");
        Custom = new("Custom");

        SubscribeToModel();
        Rebuild();
    }



    // ui events



    partial void OnSearchTextChanged(string value) => Rebuild();



    // the empty box at the bottom was left (CommitOnLeave): a custom table with the name typed in it, opened, and the box
    // empties for the next one
    public void Commit()
    {
        var name = NewTableName.Trim();
        if (name == "" || !_model.TryCreateTable(name)) return;

        NewTableName = "";
        Open(name);
    }



    // model events



    private void OnTablesChanged(TableModel table) => Rebuild();



    private void OnTableRenamed(TableModel table, string oldName) => Rebuild();



    // helpers



    private void Open(string name)
    {
        _open(name);
        Rebuild();
    }



    // the tables matching the search under each heading, the open one highlighted
    private void Rebuild()
    {
        var search = SearchText.Trim();
        var open = _openTable();
        TableItemViewModel Item(TableModel t) => new(t.Name, Open) { IsOpen = string.Equals(t.Name, open, StringComparison.OrdinalIgnoreCase) };
        var matching = _model.Tables.Where(t => t.Name.Contains(search, StringComparison.OrdinalIgnoreCase)).OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();

        BuiltIn.Tables = matching.Where(t => t.IsBuiltIn).Select(Item).ToList();
        Custom.Tables = matching.Where(t => !t.IsBuiltIn).Select(Item).ToList();
    }



    // subscriptions



    private void SubscribeToModel()
    {
        _model.TableAdded += OnTablesChanged;
        _model.TableRemoved += OnTablesChanged;
        _model.TableRenamed += OnTableRenamed;
    }



    private void UnsubscribeFromModel()
    {
        _model.TableAdded -= OnTablesChanged;
        _model.TableRemoved -= OnTablesChanged;
        _model.TableRenamed -= OnTableRenamed;
    }



    // disposal



    public override void Dispose() => UnsubscribeFromModel();
}
