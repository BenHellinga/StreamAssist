using System;
using System.Collections.Generic;
using System.Linq;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Chatbot.Model;



// the rows of a Match Text block: there's always exactly one empty row at the end (typing in it adds the next),
// and a row disappears once its pattern is empty and it has no binding or block after it
public sealed class MatchTextRowList : BlockSlotBase
{
    // private



    private readonly List<MatchTextRow> _rows;



    // public



    public IReadOnlyList<MatchTextRow> Rows => _rows;
    public override IReadOnlyList<BlockModel> Blocks => [];
    public override IReadOnlyList<BlockSlotBase> InnerSlots => _rows.Select(r => (BlockSlotBase)r.Target).ToList();



    // events



    public event Action<MatchTextRow, int>? RowInserted;
    public event Action<MatchTextRow>? RowRemoved;



    // constructor



    public MatchTextRowList(string label) : base(label, typeof(ChatBlock))
    {
        _rows = [];
        Normalize();
    }



    // row management



    // removes empty rows (except the last) and makes sure the last row is an empty one
    private void Normalize()
    {
        // the row just above the trailing one was emptied (e.g. backspacing a typo): drop the trailing row instead,
        // so the row being typed in stays put and keeps its cursor
        while (_rows.Count >= 2 && _rows[^1].IsEmpty && _rows[^2].IsEmpty)
            RemoveRow(_rows[^1]);

        for (int i = _rows.Count - 2; i >= 0; i--)
            if (_rows[i].IsEmpty) RemoveRow(_rows[i]);

        if (_rows.Count == 0 || !_rows[^1].IsEmpty)
            InsertRow(new MatchTextRow(), _rows.Count);
    }



    private void InsertRow(MatchTextRow row, int index)
    {
        _rows.Insert(index, row);
        SubscribeToRow(row);

        RowInserted?.Invoke(row, index);
        RaiseChanged();
    }



    private void RemoveRow(MatchTextRow row)
    {
        if (!_rows.Remove(row)) return;
        UnsubscribeFromRow(row);

        RowRemoved?.Invoke(row);
        RaiseChanged();
        row.Dispose();
    }



    public override void RemoveBlock(BlockModel block)
    {
        foreach (var row in _rows.ToArray())
            row.Target.RemoveBlock(block);
    }



    // model callbacks



    private void OnRowPatternChanged()
    {
        RaiseChanged();
        Normalize();
    }



    private void OnRowTargetContentsChanged() => Normalize();



    private void OnRowSlotChanged() => RaiseChanged();



    // helpers



    private void SubscribeToRow(MatchTextRow row)
    {
        row.PatternChanged += OnRowPatternChanged;
        row.Target.ContentsChanged += OnRowTargetContentsChanged;
        row.Target.Changed += OnRowSlotChanged;
    }



    private void UnsubscribeFromRow(MatchTextRow row)
    {
        row.PatternChanged -= OnRowPatternChanged;
        row.Target.ContentsChanged -= OnRowTargetContentsChanged;
        row.Target.Changed -= OnRowSlotChanged;
    }



    // json (owners save the list through these)



    // the trailing empty row isn't saved, it's added back on load
    public List<MatchTextRowData> GetData() => _rows.Where(r => !r.IsEmpty).Select(r => r.GetData()).ToList();



    public void LoadData(List<MatchTextRowData> data)
    {
        foreach (var row in _rows.ToArray())
            RemoveRow(row);

        foreach (var item in data)
        {
            var row = new MatchTextRow();
            row.LoadData(item);

            if (row.IsEmpty) row.Dispose();
            else InsertRow(row, _rows.Count);
        }

        Normalize();
    }



    // disposal



    public override void Dispose()
    {
        foreach (var row in _rows)
            row.Dispose();
    }
}
