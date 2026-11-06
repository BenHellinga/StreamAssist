using System;
using System.Collections.Generic;
using System.Linq;
using StreamAssist.Core.Utilities;

namespace StreamAssist.Components.Dashboard.Model;



// an indicator or variable dock's rows: like button rows, there's always exactly one empty row at the end (typing a name in it
// adds the next), and a row goes once its name is emptied (see GrowingList)
public sealed class DockNameList : IDisposable
{
    // private



    private readonly GrowingList<DockNameRow> _rows;



    // public



    public IReadOnlyList<DockNameRow> Rows => _rows.Items;



    // events



    public event Action<DockNameRow, int>? RowInserted
    {
        add => _rows.ItemInserted += value;
        remove => _rows.ItemInserted -= value;
    }

    public event Action<DockNameRow>? RowRemoved
    {
        add => _rows.ItemRemoved += value;
        remove => _rows.ItemRemoved -= value;
    }

    public event Action? Changed; // a name or row changed (saved)



    // constructor



    public DockNameList()
    {
        _rows = new(() => new DockNameRow(), row => row.IsEmpty, SubscribeToRow, UnsubscribeFromRow);
        _rows.Changed += OnRowsChanged;
    }



    // model callbacks



    private void OnRowsChanged() => Changed?.Invoke();



    private void OnRowNameChanged()
    {
        Changed?.Invoke();
        _rows.Normalize();
    }



    // helpers



    private void SubscribeToRow(DockNameRow row) => row.NameChanged += OnRowNameChanged;



    private void UnsubscribeFromRow(DockNameRow row) => row.NameChanged -= OnRowNameChanged;



    // json (the trailing empty row isn't saved, it's added back on load)



    public List<string> GetData() => _rows.NonEmptyItems.Select(r => r.Name).ToList();



    public void LoadData(List<string> data) => _rows.Load(data.Select(name =>
    {
        var row = new DockNameRow();
        row.SetName(name);
        return row;
    }));



    // disposal



    public void Dispose()
    {
        _rows.Changed -= OnRowsChanged;
        _rows.Dispose();
    }
}
