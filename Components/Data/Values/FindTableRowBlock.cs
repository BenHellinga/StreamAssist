using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Data.Values;



public class FindTableRowBlock : ValueBlock<int>
{
    // info



    [JsonIgnore] public override string Name => "Find Table Row";
    [JsonIgnore] public override string Description => "The id of the first row whose Column has this Value (compared as text, case ignored), or 0 if there's none, e.g. a viewer's row by {userLogin}. Table is the name of a table in the Data tab.";



    // private



    private readonly ValueSlot<string> _table;
    private readonly ValueSlot<string> _column;
    private readonly ValueSlot<string> _value;
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

    [JsonInclude] [JsonPropertyName("column")]
    private ValueSlotData JsonColumn
    {
        get => _column.GetData();
        set => _column.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("value")]
    private ValueSlotData JsonValue
    {
        get => _value.GetData();
        set => _value.LoadData(value);
    }



    // constructor



    public FindTableRowBlock()
    {
        _table = new("Table");
        _column = new("Column");
        _value = new("Value");
        _slots = [_table, _column, _value];

        TrackSlots();
    }



    // compute



    protected override async Task<int> ComputeAsync(TriggerContext context)
    {
        var data = GlobalContext.DataModel ?? throw Fail("data not set up");
        var tableName = await _table.EvaluateAsync(context);
        var table = data.FindTable(tableName) ?? throw Fail($"no table '{tableName}'");
        var columnName = await _column.EvaluateAsync(context);
        var column = table.FindColumn(columnName) ?? throw Fail($"{table.Name} has no column '{columnName}'");
        var wanted = (await _value.EvaluateAsync(context)).Trim();

        var row = table.Rows.FirstOrDefault(r => string.Equals(ValueTypes.ToText(r.Get(column.Name) ?? "").Trim(), wanted, StringComparison.OrdinalIgnoreCase));
        return row?.Id ?? 0;
    }
}
