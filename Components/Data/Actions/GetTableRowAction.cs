using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Data.Actions;



public class GetTableRowAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Get Table Row";
    [JsonIgnore] public override string Description => "Reads a whole row into parameters, one per column named after it (e.g. {quote}, {date}, and {id}), with the Prefix in front if there is one ({q_quote}). Empty cells read as empty text. Only for this run, nothing is kept.";



    // private



    private readonly ValueSlot<string> _table;
    private string _prefix;
    private readonly ValueSlot<int> _row;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    // the table's name as typed (a {parameter} or a block placed in the slot works too)
    [JsonIgnore] public string Table
    {
        get => _table.Text;
        set => _table.SetText(value);
    }

    // put before every column's name, so two rows (or a row and the trigger's own parameters) don't overwrite each other
    [ConfigurableParameter("Prefix", ConfiguratorType.Text)] [JsonPropertyName("prefix")]
    public string Prefix
    {
        get => _prefix;
        set => SetParameter(ref _prefix, value);
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



    public GetTableRowAction()
    {
        _table = new("Table");
        _prefix = "";
        _row = new("Row");
        _slots = [_table, _row];

        _row.SetText("{rowId}");

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var data = GlobalContext.DataModel ?? throw Fail("data not set up");
        var tableName = await _table.EvaluateAsync(context);
        var table = data.FindTable(tableName) ?? throw Fail($"no table '{tableName}'");
        var id = await _row.EvaluateAsync(context);
        var row = table.FindRow(id) ?? throw Fail($"{table.Name} has no row {id}");

        var prefix = _prefix.Trim();
        if (prefix != "" && !TriggerContext.IsValidName(prefix)) throw Fail($"prefix '{prefix}' isn't a valid name, use letters, digits, _");

        context.SetParameter(prefix + "id", row.Id.ToString());
        foreach (var column in table.Columns)
            context.SetParameter(prefix + column.Name, ValueTypes.ToText(row.Get(column.Name) ?? ""));
    }
}
