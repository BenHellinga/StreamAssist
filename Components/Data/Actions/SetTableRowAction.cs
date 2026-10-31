using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Data.Actions;



public class SetTableRowAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Set Table Row";
    [JsonIgnore] public override string Description => "Set Table Value for several cells of one row: a column name on each row with its value to the right, set top to bottom. Each value is converted to its column's type, and the first one that doesn't fit stops the run (the ones above it are already set).";



    // private



    private readonly ValueSlot<string> _table;
    private readonly ValueSlot<int> _row;
    private readonly ValueSlotPairList _values;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    // the table's name as typed (a {parameter} or a block placed in the slot works too)
    [JsonIgnore] public string Table
    {
        get => _table.Text;
        set => _table.SetText(value);
    }

    [JsonIgnore] public ValueSlotPairList Values => _values;
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

    [JsonInclude] [JsonPropertyName("values")]
    private List<ValueSlotPairData> JsonValues
    {
        get => _values.GetData();
        set => _values.LoadData(value);
    }



    // constructor



    public SetTableRowAction()
    {
        _table = new("Table");
        _row = new("Row");
        _values = new("", "Column…");
        _slots = [_table, _row, _values];

        _row.SetText("{rowId}");

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var data = GlobalContext.DataModel ?? throw Fail("data not set up");
        var table = await _table.EvaluateAsync(context);
        var row = await _row.EvaluateAsync(context);

        foreach (var (column, kind, value) in await _values.EvaluateFilledAsync(context))
        {
            if (column == "") throw Fail("a row needs a column name");
            if (!data.TrySetValue(table, row, column, value, kind)) throw new BlockFailedException("value not set");
        }
    }
}
