using StreamAssist.Components.CSharp.Api;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Data.Actions;



public class GetTableRowAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Get Table Row";
    [JsonIgnore] public override string Description => "Reads a whole row into parameters, one per column named after it (e.g. {quote}, {date}, and {id}), with the Prefix in front if there is one ({q_quote}). Empty cells read as empty text. Only for this run, nothing is kept.";
    [JsonIgnore] public override string? Subgroup => "Read";



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
        var table = await _table.EvaluateAsync(context);
        var id = await _row.EvaluateAsync(context);
        var prefix = _prefix.Trim();
        if (prefix != "" && !TriggerContext.IsValidName(prefix)) throw Fail($"prefix '{prefix}' isn't a valid name, use letters, digits, _");

        var row = Api(context).Data.GetRow(table, id) ?? throw Fail($"{DataScriptApi.Shape(table).Name} has no row {id}");
        context.SetParameter(prefix + "id", row.Id.ToString());
        foreach (var (column, value) in row.Values)
            context.SetParameter(prefix + column, ValueTypes.ToText(value ?? ""));
    }
}
