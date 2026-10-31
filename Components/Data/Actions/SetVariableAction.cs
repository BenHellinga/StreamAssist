using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Data.Actions;



public class SetVariableAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Set Variable";
    [JsonIgnore] public override string Description => "Sets a variable (made in the Data tab, kept after the app closes) to a value, typed or from a block. It's converted to the variable's type and fails if it doesn't fit. For counting, use Change Variable.";



    // private



    private string _variable;
    private readonly AnyValueSlot _value;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Variable", ConfiguratorType.Menu)] [JsonPropertyName("variable")]
    public string Variable
    {
        get => _variable;
        set
        {
            SetParameter(ref _variable, value);
        }
    }

    public static List<string> GetVariableMenuItems() => GlobalContext.DataModel?.VariableNames() ?? [];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("value")]
    private ValueSlotData JsonValue
    {
        get => _value.GetData();
        set => _value.LoadData(value);
    }



    // constructor



    public SetVariableAction()
    {
        _variable = "";
        _value = new("Value");
        _slots = [_value];

        TrackSlots();
    }



    // execute



    // the model logs why when it refuses, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var data = GlobalContext.DataModel ?? throw Fail("data not set up");
        var (kind, value) = await _value.EvaluateRawAsync(context);

        if (!data.TrySetVariable(_variable, value, kind)) throw new BlockFailedException("variable not set");
    }
}
