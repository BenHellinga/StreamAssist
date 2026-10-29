using System;
using System.Collections.Generic;
using System.Linq;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Core.Utilities;

namespace StreamAssist.Components.Chatbot.Model;



// the rows of a Match Text block: there's always exactly one empty row at the end (typing in it adds the next),
// and a row disappears once its pattern is empty and it has no binding or block after it (see GrowingList)
public sealed class MatchTextRowList : BlockSlotBase
{
    // private



    private readonly GrowingList<MatchTextRow> _rows;



    // public



    public IReadOnlyList<MatchTextRow> Rows => _rows.Items;
    public override IReadOnlyList<BlockModel> Blocks => [];
    public override IReadOnlyList<BlockSlotBase> InnerSlots => _rows.Items.Select(r => (BlockSlotBase)r.Target).ToList();



    // events



    public event Action<MatchTextRow, int>? RowInserted
    {
        add => _rows.ItemInserted += value;
        remove => _rows.ItemInserted -= value;
    }

    public event Action<MatchTextRow>? RowRemoved
    {
        add => _rows.ItemRemoved += value;
        remove => _rows.ItemRemoved -= value;
    }



    // constructor



    public MatchTextRowList(string label) : base(label, typeof(ChatBlock))
    {
        _rows = new(() => new MatchTextRow(), row => row.IsEmpty, SubscribeToRow, UnsubscribeFromRow);
        _rows.Changed += RaiseChanged;
    }



    // public api



    // fills the empty row at the end with this pattern (a new empty one appears below it) and returns it
    public MatchTextRow AddRow(string pattern)
    {
        var row = _rows.Items[^1];
        row.SetPattern(pattern);
        return row;
    }



    // removes a row along with its binding or the block after it
    public void RemoveRow(MatchTextRow row)
    {
        if (!_rows.Items.Contains(row)) return;

        row.SetPattern("");
        row.Target.Unbind();
        if (row.Target.Child.Block is { } child) row.Target.RemoveBlock(child); // the now empty row disappears
    }



    public override void RemoveBlock(BlockModel block)
    {
        foreach (var row in _rows.Items.ToArray())
            row.Target.RemoveBlock(block);
    }



    // model callbacks



    private void OnRowPatternChanged()
    {
        RaiseChanged();
        _rows.Normalize();
    }



    private void OnRowTargetContentsChanged() => _rows.Normalize();



    private void OnRowTargetChanged() => RaiseChanged();



    // helpers



    private void SubscribeToRow(MatchTextRow row)
    {
        row.PatternChanged += OnRowPatternChanged;
        row.Target.ContentsChanged += OnRowTargetContentsChanged;
        row.Target.Changed += OnRowTargetChanged;
    }



    private void UnsubscribeFromRow(MatchTextRow row)
    {
        row.PatternChanged -= OnRowPatternChanged;
        row.Target.ContentsChanged -= OnRowTargetContentsChanged;
        row.Target.Changed -= OnRowTargetChanged;
    }



    // json (owners save the list through these)



    // the trailing empty row isn't saved, it's added back on load
    public List<MatchTextRowData> GetData() => _rows.NonEmptyItems.Select(r => r.GetData()).ToList();



    public void LoadData(List<MatchTextRowData> data) => _rows.Load(data.Select(item =>
    {
        var row = new MatchTextRow();
        row.LoadData(item);
        return row;
    }));



    // disposal



    public override void Dispose()
    {
        _rows.Changed -= RaiseChanged;
        _rows.Dispose();
    }
}
