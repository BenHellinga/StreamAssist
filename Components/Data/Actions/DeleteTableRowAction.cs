using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Data.Actions;



public class DeleteTableRowAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Delete Table Row";
    [JsonIgnore] public override string Description => "Deletes a row of a table by its id. Table is the name of a table in the Data tab.";



    // private



    private readonly ValueSlot<string> _table;
    private readonly ValueSlot<int> _row;
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



    // constructor



    public DeleteTableRowAction()
    {
        _table = new("Table");
        _row = new("Row");
        _slots = [_table, _row];

        _row.SetText("{rowId}");

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var data = GlobalContext.DataModel ?? throw Fail("data not set up");
        var row = await _row.EvaluateAsync(context);

        if (!data.TryDeleteRow(await _table.EvaluateAsync(context), row)) throw new BlockFailedException("row not deleted");
    }
}
