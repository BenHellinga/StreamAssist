using System;
using System.Collections.Generic;
using System.Linq;

namespace StreamAssist.Components.Chatbot.Model;



// the channel point rewards the chatbot reacts to: like Match Text rows, there's always exactly one empty row at the end
// (typing in it adds the next), and a row disappears once its title is empty and it has no binding
public sealed class RedeemList : IDisposable
{
    // private



    private readonly List<RedeemRow> _rows;



    // public



    public IReadOnlyList<RedeemRow> Rows => _rows;



    // events



    public event Action<RedeemRow, int>? RowInserted;
    public event Action<RedeemRow>? RowRemoved;
    public event Action? Changed; // a title, binding or row changed (saved to chatbot.json)



    // constructor



    public RedeemList()
    {
        _rows = [];
        Normalize();
    }



    // public api



    // runs the first row with this title whose binding is enabled, false if none did
    public bool TryRun(string rewardTitle, IReadOnlyDictionary<string, string> parameters)
    {
        foreach (var row in _rows.ToArray())
            if (row.Matches(rewardTitle) && row.Target.TryRun(parameters)) return true;

        return false;
    }



    // row management



    // removes empty rows (except the last) and makes sure the last row is an empty one
    private void Normalize()
    {
        // the row just above the trailing one was emptied: drop the trailing row instead, so the edited row keeps its cursor
        while (_rows.Count >= 2 && _rows[^1].IsEmpty && _rows[^2].IsEmpty)
            RemoveRow(_rows[^1]);

        for (int i = _rows.Count - 2; i >= 0; i--)
            if (_rows[i].IsEmpty) RemoveRow(_rows[i]);

        if (_rows.Count == 0 || !_rows[^1].IsEmpty)
            InsertRow(new RedeemRow(), _rows.Count);
    }



    private void InsertRow(RedeemRow row, int index)
    {
        _rows.Insert(index, row);
        SubscribeToRow(row);

        RowInserted?.Invoke(row, index);
        Changed?.Invoke();
    }



    private void RemoveRow(RedeemRow row)
    {
        if (!_rows.Remove(row)) return;
        UnsubscribeFromRow(row);

        RowRemoved?.Invoke(row);
        Changed?.Invoke();
        row.Dispose();
    }



    // model callbacks



    private void OnRowTitleChanged()
    {
        Changed?.Invoke();
        Normalize();
    }



    private void OnRowTargetContentsChanged() => Normalize();



    private void OnRowTargetChanged() => Changed?.Invoke();



    // helpers



    private void SubscribeToRow(RedeemRow row)
    {
        row.TitleChanged += OnRowTitleChanged;
        row.Target.ContentsChanged += OnRowTargetContentsChanged;
        row.Target.Changed += OnRowTargetChanged;
    }



    private void UnsubscribeFromRow(RedeemRow row)
    {
        row.TitleChanged -= OnRowTitleChanged;
        row.Target.ContentsChanged -= OnRowTargetContentsChanged;
        row.Target.Changed -= OnRowTargetChanged;
    }



    // json (the chatbot model saves the list through these)



    // the trailing empty row isn't saved, it's added back on load
    public List<RedeemRowData> GetData() => _rows.Where(r => !r.IsEmpty).Select(r => r.GetData()).ToList();



    public void LoadData(List<RedeemRowData> data)
    {
        foreach (var row in _rows.ToArray())
            RemoveRow(row);

        foreach (var item in data)
        {
            var row = new RedeemRow();
            row.LoadData(item);

            if (row.IsEmpty) row.Dispose();
            else InsertRow(row, _rows.Count);
        }

        Normalize();
    }



    // disposal



    public void Dispose()
    {
        foreach (var row in _rows)
        {
            UnsubscribeFromRow(row);
            row.Dispose();
        }
    }
}
