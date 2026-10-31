using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Data.Values;



public class RandomTableRowBlock : ValueBlock<int>
{
    // info



    [JsonIgnore] public override string Name => "Random Table Row";
    [JsonIgnore] public override string Description => "The id of a row picked at random (e.g. for !quote), or 0 if the table is empty. Table is the name of a table in the Data tab.";



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



    public RandomTableRowBlock()
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
        return table.Rows.Count == 0 ? 0 : table.Rows[Random.Shared.Next(table.Rows.Count)].Id;
    }
}
