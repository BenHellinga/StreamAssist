using System;
using System.Collections.Generic;
using System.Linq;
using StreamAssist.Core.Utilities;

namespace StreamAssist.Components.Chatbot.Model;



// the channel point rewards the chatbot reacts to: like Match Text rows, there's always exactly one empty row at the end
// (typing in it adds the next), and a row disappears once its title is empty and it has no binding (see GrowingList)
public sealed class RedeemList : IDisposable
{
    // private



    private readonly GrowingList<RedeemRow> _rows;



    // public



    public IReadOnlyList<RedeemRow> Rows => _rows.Items;



    // events



    public event Action<RedeemRow, int>? RowInserted
    {
        add => _rows.ItemInserted += value;
        remove => _rows.ItemInserted -= value;
    }

    public event Action<RedeemRow>? RowRemoved
    {
        add => _rows.ItemRemoved += value;
        remove => _rows.ItemRemoved -= value;
    }

    public event Action? Changed; // a title, binding or row changed (saved to chatbot.json)



    // constructor



    public RedeemList()
    {
        _rows = new(() => new RedeemRow(), row => row.IsEmpty, SubscribeToRow, UnsubscribeFromRow);
        _rows.Changed += OnRowsChanged;
    }



    // public api



    // runs the first row with this title whose binding is enabled, false if none did
    public bool TryRun(string rewardTitle, IReadOnlyDictionary<string, string> parameters)
    {
        foreach (var row in _rows.Items.ToArray())
            if (row.Matches(rewardTitle) && row.Target.TryRun(parameters)) return true;

        return false;
    }



    // model callbacks



    private void OnRowsChanged() => Changed?.Invoke();



    private void OnRowTitleChanged()
    {
        Changed?.Invoke();
        _rows.Normalize();
    }



    private void OnRowTargetContentsChanged() => _rows.Normalize();



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
    public List<RedeemRowData> GetData() => _rows.NonEmptyItems.Select(r => r.GetData()).ToList();



    public void LoadData(List<RedeemRowData> data) => _rows.Load(data.Select(item =>
    {
        var row = new RedeemRow();
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
