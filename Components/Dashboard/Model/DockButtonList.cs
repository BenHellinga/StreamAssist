using System;
using System.Collections.Generic;
using System.Linq;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Core.Utilities;

namespace StreamAssist.Components.Dashboard.Model;



// a button dock's rows: like redeem rows, there's always exactly one empty row at the end (typing a name in it adds
// the next), and a row goes once its name is empty and it has no binding (see GrowingList)
public sealed class DockButtonList : IDisposable
{
    // private



    private readonly GrowingList<DockButton> _rows;



    // public



    public IReadOnlyList<DockButton> Rows => _rows.Items;



    // events



    public event Action<DockButton, int>? RowInserted
    {
        add => _rows.ItemInserted += value;
        remove => _rows.ItemInserted -= value;
    }

    public event Action<DockButton>? RowRemoved
    {
        add => _rows.ItemRemoved += value;
        remove => _rows.ItemRemoved -= value;
    }

    public event Action<DockButton, int, int>? RowMoved // the row, where it was, where it is now
    {
        add => _rows.ItemMoved += value;
        remove => _rows.ItemMoved -= value;
    }

    public event Action? Changed; // a name, binding or row changed



    // constructor



    public DockButtonList()
    {
        _rows = new(() => new DockButton(), row => row.IsEmpty, SubscribeToRow, UnsubscribeFromRow);
        _rows.Changed += OnRowsChanged;
    }



    // public api



    // moves a filled button to just before the one now at index (the list's length = last), e.g. dragged there to
    // reorder. The empty one at the end can't move
    public bool TryMoveRow(DockButton item, int index)
    {
        if (!_rows.Items.Contains(item))
        {
            AppLog.WriteError("can't move button, not in this dock");
            return false;
        }

        if (!_rows.Move(item, index))
        {
            AppLog.WriteError("can't move an empty button");
            return false;
        }

        return true;
    }



    // the row and its binding go (its × button)
    public void Remove(DockButton row)
    {
        if (!_rows.Items.Contains(row)) return;

        row.Unbind();
        row.SetName("");
    }



    // model callbacks



    private void OnRowsChanged() => Changed?.Invoke();



    private void OnRowChanged() => Changed?.Invoke();



    private void OnRowContentsChanged() => _rows.Normalize();



    // helpers



    private void SubscribeToRow(DockButton row)
    {
        row.Changed += OnRowChanged;
        row.NameChanged += OnRowContentsChanged;
        row.BindableChanged += OnRowContentsChanged;
    }



    private void UnsubscribeFromRow(DockButton row)
    {
        row.Changed -= OnRowChanged;
        row.NameChanged -= OnRowContentsChanged;
        row.BindableChanged -= OnRowContentsChanged;
    }



    // json (the trailing empty row isn't saved, it's added back on load)



    public List<DockButtonData> GetData() => _rows.NonEmptyItems.Select(r => r.GetData()).ToList();



    public void LoadData(List<DockButtonData> data) => _rows.Load(data.Select(item =>
    {
        var row = new DockButton();
        row.LoadData(item);
        return row;
    }));



    // disposal



    public void Dispose()
    {
        _rows.Changed -= OnRowsChanged;
        _rows.Dispose();
    }
}
