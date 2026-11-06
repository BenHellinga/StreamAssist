using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Dashboard.Model;
using StreamAssist.Components.Data.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Dashboard.UI;



// a variable dock's body: its rows (the last one always empty, typing a name in it adds the next), each showing and
// editing the Data tab's variable of that name. Scripts read and write the same variable ({var.name}, Set Variable),
// so this follows the data model's events: a row is looked up again when its variable changes
public sealed class VariableDockViewModel : ViewModelBase
{
    // private



    private readonly DockModel _dock;
    private readonly DataModel? _data; // null only where there's no data (tests): the rows then show no values



    // public



    public ObservableCollection<VariableRowViewModel> Rows { get; }



    // constructor



    public VariableDockViewModel(DockModel dock, DataModel? data)
    {
        _dock = dock;
        _data = data;
        Rows = new(_dock.Variables.Rows.Select(row => new VariableRowViewModel(row, _data, _dock.IsDisabled)));

        _dock.IsDisabledChanged += OnDockIsDisabledChanged;
        _dock.Variables.RowInserted += OnRowInserted;
        _dock.Variables.RowRemoved += OnRowRemoved;
        SubscribeToData(_data);
    }



    // model events



    private void OnRowInserted(DockNameRow row, int index) => Rows.Insert(index, new VariableRowViewModel(row, _data, _dock.IsDisabled));



    // off: the values stay as they are and can't be typed in; back on, they catch up with the Data tab
    private void OnDockIsDisabledChanged(bool isDisabled)
    {
        foreach (var row in Rows) row.IsFrozen = isDisabled;
        if (!isDisabled) RefreshAll();
    }



    private void OnRowRemoved(DockNameRow row)
    {
        var viewModel = Rows.FirstOrDefault(r => r.Represents(row));
        if (viewModel == null) return;

        Rows.Remove(viewModel);
        viewModel.Dispose();
    }



    // only the rows naming the variable that changed are looked up again, so a value being typed in another row
    // isn't put back; a renamed variable can change which rows find one, so then all are
    private void OnDataValueChanged(TableModel table, TableRow row, TableColumn column)
    {
        if (table.Name != DataModel.VariablesTable) return;

        if (column.Name == "name") RefreshAll();
        else Refresh(row.Get("name") as string ?? "");
    }



    private void OnDataRowAddedOrRemoved(TableModel table, TableRow _)
    {
        if (table.Name == DataModel.VariablesTable) RefreshAll();
    }



    // helpers



    private void Refresh(string variable)
    {
        if (_dock.IsDisabled) return;
        foreach (var row in Rows.Where(r => r.Shows(variable)))
            row.Refresh();
    }



    private void RefreshAll()
    {
        if (_dock.IsDisabled) return;
        foreach (var row in Rows)
            row.Refresh();
    }



    private void SubscribeToData(DataModel? data)
    {
        if (data == null) return;

        data.ValueChanged += OnDataValueChanged;
        data.RowAdded += OnDataRowAddedOrRemoved;
        data.RowRemoved += OnDataRowAddedOrRemoved;
    }



    private void UnsubscribeFromData(DataModel? data)
    {
        if (data == null) return;

        data.ValueChanged -= OnDataValueChanged;
        data.RowAdded -= OnDataRowAddedOrRemoved;
        data.RowRemoved -= OnDataRowAddedOrRemoved;
    }



    // disposal



    public override void Dispose()
    {
        _dock.IsDisabledChanged -= OnDockIsDisabledChanged;
        _dock.Variables.RowInserted -= OnRowInserted;
        _dock.Variables.RowRemoved -= OnRowRemoved;
        UnsubscribeFromData(_data);

        foreach (var row in Rows) row.Dispose();
    }
}



// one row: the variable's name | its value, applied when the value box is left (a multi-line box keeps Enter for new
// lines). A name with no variable yet is grey, and typing a value makes it (a Text variable); a value that doesn't fit
// the variable's type is refused (logged) and the box goes back to the variable's value
public sealed partial class VariableRowViewModel : ViewModelBase, ICommitOnLeave
{
    // private



    private readonly DockNameRow _row;
    private readonly DataModel? _data;

    [ObservableProperty] private string _name;
    [ObservableProperty] private string _valueInput;
    [ObservableProperty] private bool _isMissing; // no variable has this name
    [ObservableProperty] private bool _showValue; // not on the empty last row
    [ObservableProperty] private bool _isFrozen; // its dock is off: the value can't be typed in



    // constructor



    public VariableRowViewModel(DockNameRow row, DataModel? data, bool isFrozen)
    {
        _row = row;
        _data = data;
        _isFrozen = isFrozen;
        _name = _row.Name;
        _valueInput = "";

        Refresh();

        _row.NameChanged += OnRowNameChanged;
    }



    // public api



    public bool Represents(DockNameRow row) => row == _row;



    public bool Shows(string variable) => string.Equals(_row.Name.Trim(), variable.Trim(), StringComparison.OrdinalIgnoreCase);



    // the variable's value as saved (its text), "" with none
    public void Refresh()
    {
        var name = _row.Name.Trim();
        IsMissing = name != "" && _data?.VariableKind(name) == null;
        ShowValue = !_row.IsEmpty;
        ValueInput = name == "" ? "" : _data?.GetVariableText(name) ?? "";
    }



    // ui events



    // typing in the name box goes straight to the model
    partial void OnNameChanged(string value) => _row.SetName(value ?? "");



    public void Commit()
    {
        var name = _row.Name.Trim();
        if (_data == null || name == "" || IsFrozen) return;

        if (_data.VariableKind(name) == null)
        {
            if (ValueInput != "") _data.SetVariableText(name, ValueInput);
        }
        else if (ValueInput != _data.GetVariableText(name)) _data.TrySetVariable(name, ValueInput);

        Refresh(); // shows what was kept: the new value, or the old one if it was refused
    }



    // model events



    private void OnRowNameChanged()
    {
        Name = _row.Name;
        Refresh();
    }



    // disposal



    public override void Dispose() => _row.NameChanged -= OnRowNameChanged;
}
