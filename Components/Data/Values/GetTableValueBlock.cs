using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Data.Values;



public class GetTableValueBlock : AdaptableValueBlock
{
    // info



    [JsonIgnore] public override string Name => "Get Table Value";
    [JsonIgnore] public override string Description => "One cell: a row (by its id) and a column (by its name), read as text, a number or true/false. An empty cell reads as empty text, 0 or false. Table is the name of a table in the Data tab.";



    // private



    private readonly ValueSlot<string> _table;
    private readonly ValueSlot<int> _row;
    private readonly ValueSlot<string> _column;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    // the table's name as typed (a {parameter} or a block placed in the slot works too)
    [JsonIgnore] public string Table
    {
        get => _table.Text;
        set => _table.SetText(value);
    }

    // what it's read as, placing it in a slot picks the slot's type (saved after the setting, so a loaded block keeps it)
    [ConfigurableParameter("As", ConfiguratorType.Menu)] [JsonPropertyName("kind")]
    public string As
    {
        get => KindName;
        set => KindName = value;
    }

    public static List<string> GetAsMenuItems() => ["Text", "Int", "Double", "Bool"];

    [JsonIgnore] public override IReadOnlyList<ValueKind> AllowedKinds => [ValueKind.Text, ValueKind.Int, ValueKind.Double, ValueKind.Bool];
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



    // constructor



    public GetTableValueBlock() : base(ValueKind.Text)
    {
        _table = new("Table");
        _row = new("Row");
        _column = new("Column");
        _slots = [_table, _row, _column];

        _row.SetText("{rowId}");

        TrackSlots();
    }



    // compute



    protected override async Task<object> ComputeAsync(TriggerContext context)
    {
        var data = GlobalContext.DataModel ?? throw Fail("data not set up");
        var tableName = await _table.EvaluateAsync(context);
        var table = data.FindTable(tableName) ?? throw Fail($"no table '{tableName}'");
        var id = await _row.EvaluateAsync(context);
        var columnName = await _column.EvaluateAsync(context);
        var column = table.FindColumn(columnName) ?? throw Fail($"{table.Name} has no column '{columnName}'");
        var row = table.FindRow(id) ?? throw Fail($"{table.Name} has no row {id}");

        var value = row.Get(column.Name);
        if (value == null) return Kind switch { ValueKind.Int => 0, ValueKind.Double => 0.0, ValueKind.Bool => false, _ => "" };
        if (!ValueTypes.TryConvert(value, column.Kind, Kind, out var result, out var error)) throw Fail(error);
        return result;
    }
}
