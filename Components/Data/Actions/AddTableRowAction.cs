using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Data.Actions;



public class AddTableRowAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Add Table Row";
    [JsonIgnore] public override string Description => "Adds an empty row at the end of a table and puts its id in a parameter ({rowId} by default) for the blocks after it, e.g. Set Table Value. Table is the name of a table in the Data tab.";



    // private



    private readonly ValueSlot<string> _table;
    private string _idName;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    // the table's name as typed (a {parameter} or a block placed in the slot works too)
    [JsonIgnore] public string Table
    {
        get => _table.Text;
        set => _table.SetText(value);
    }

    [ConfigurableParameter("Save Id As", ConfiguratorType.Text)] [JsonPropertyName("idName")]
    public string IdName
    {
        get => _idName;
        set => SetParameter(ref _idName, value);
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



    public AddTableRowAction()
    {
        _table = new("Table");
        _idName = "rowId";
        _slots = [_table];

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var data = GlobalContext.DataModel ?? throw Fail("data not set up");
        var name = _idName.Trim();
        if (name != "" && !TriggerContext.IsValidName(name)) throw Fail($"'{name}' isn't a valid name, use letters, digits, _");

        if (!data.TryAddRow(await _table.EvaluateAsync(context), out var row)) throw new BlockFailedException("row not added");
        if (name != "") context.SetParameter(name, row.Id.ToString());
    }
}
