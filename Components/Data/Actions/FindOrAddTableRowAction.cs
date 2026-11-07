using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Data.Actions;



public class FindOrAddTableRowAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Find Or Add Table Row";
    [JsonIgnore] public override string Description => "Finds the first row whose Column has this Value (as text, case ignored), or adds a row with it if there's none, and puts its id in a parameter ({rowId} by default). E.g. a viewer's row by {userLogin}: then Change Table Value adds to it, an empty cell counting as 0.";
    [JsonIgnore] public override string? Subgroup => "Change";



    // private



    private string _idName;
    private readonly ValueSlot<string> _table;
    private readonly ValueSlot<string> _column;
    private readonly ValueSlot<string> _value;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Save Id As", ConfiguratorType.Text)] [JsonPropertyName("idName")]
    public string IdName
    {
        get => _idName;
        set => SetParameter(ref _idName, value);
    }

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



    public FindOrAddTableRowAction()
    {
        _idName = "rowId";
        _table = new("Table");
        _column = new("Column");
        _value = new("Value");
        _slots = [_table, _column, _value];

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var data = GlobalContext.DataModel ?? throw Fail("data not set up");
        var name = _idName.Trim();
        if (!TriggerContext.IsValidName(name)) throw Fail($"'{name}' isn't a valid name, use letters, digits, _");

        var table = await _table.EvaluateAsync(context);
        var column = await _column.EvaluateAsync(context);
        var value = await _value.EvaluateAsync(context);

        // a failure is already logged by the model
        if (!data.TryFindOrAddRow(table, column, value, out var row)) throw new BlockFailedException("row not found or added");
        context.SetParameter(name, row.Id.ToString());
    }
}
