using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Data.Actions;



public class ChangeTableValueAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Change Table Value";
    [JsonIgnore] public override string Description => "Adds Amount to a number cell (negative takes away, an empty cell counts as 0), or flips a true/false one, in one step (e.g. a viewer's points). Table is the name of a table in the Data tab.";



    // private



    private readonly ValueSlot<string> _table;
    private readonly ValueSlot<int> _row;
    private readonly ValueSlot<string> _column;
    private readonly ValueSlot<double> _amount;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    // the table's name as typed (a {parameter} or a block placed in the slot works too)
    [JsonIgnore] public string Table
    {
        get => _table.Text;
        set => _table.SetText(value);
    }

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("table")]
    private ValueSlotData JsonTable
    {
        get => _table.GetData();
        set => _table.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("row")]
    private ValueSlotData JsonRow
    {
        get => _row.GetData();
        set => _row.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("column")]
    private ValueSlotData JsonColumn
    {
        get => _column.GetData();
        set => _column.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("amount")]
    private ValueSlotData JsonAmount
    {
        get => _amount.GetData();
        set => _amount.LoadData(value);
    }



    // constructor



    public ChangeTableValueAction()
    {
        _table = new("Table");
        _row = new("Row");
        _column = new("Column");
        _amount = new("Amount");
        _slots = [_table, _row, _column, _amount];

        _row.SetText("{rowId}");
        _amount.SetText("1");

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var data = GlobalContext.DataModel ?? throw Fail("data not set up");
        var row = await _row.EvaluateAsync(context);
        var column = await _column.EvaluateAsync(context);
        var amount = await _amount.EvaluateAsync(context);

        if (!data.TryChangeValue(await _table.EvaluateAsync(context), row, column, amount)) throw new BlockFailedException("value not changed");
    }
}
