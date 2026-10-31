using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Data.Values;



public class CountTableRowsBlock : ValueBlock<int>
{
    // info



    [JsonIgnore] public override string Name => "Count Table Rows";
    [JsonIgnore] public override string Description => "How many rows a table has. Table is the name of a table in the Data tab.";



    // private



    private readonly ValueSlot<string> _table;
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



    // constructor



    public CountTableRowsBlock()
    {
        _table = new("Table");
        _slots = [_table];

        TrackSlots();
    }



    // compute



    protected override async Task<int> ComputeAsync(TriggerContext context)
    {
        var data = GlobalContext.DataModel ?? throw Fail("data not set up");
        var tableName = await _table.EvaluateAsync(context);
        var table = data.FindTable(tableName) ?? throw Fail($"no table '{tableName}'");
        return table.Rows.Count;
    }
}
