using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Actions;



// turns a value (typed, or from any block) into a {name} parameter for the rest of this run, so text, formulas and
// typed values further on can use it, e.g. otherCost = Get Reward Setting, then "{otherCost}" in a message
public class SetParameterAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Set Parameter";
    [JsonIgnore] public override string Description => "Gives a value a {name} for the rest of this run: every block after it can use it, like the parameters a trigger passes in (e.g. otherCost, then {otherCost}). It's only for this run, nothing is kept. Setting a name that already exists replaces it from here on.";



    // private



    private string _parameterName;
    private readonly AnyValueSlot _value;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Name", ConfiguratorType.Text)] [JsonPropertyName("parameterName")]
    public string ParameterName
    {
        get => _parameterName;
        set => SetParameter(ref _parameterName, value);
    }

    [JsonIgnore] public AnyValueSlot Value => _value;
    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("value")]
    private ValueSlotData JsonValue
    {
        get => _value.GetData();
        set => _value.LoadData(value);
    }



    // constructor



    public SetParameterAction()
    {
        _parameterName = "";
        _value = new("Value");
        _slots = [_value];

        TrackSlots();
    }



    // execute



    // parameters are always text, so the value is stored the way it reads (e.g. 100, true, hello)
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var name = _parameterName.Trim();
        if (name == "") throw Fail("it needs a name");
        if (!TriggerContext.IsValidName(name)) throw Fail($"'{name}' can't be a parameter name, use letters, digits and _");

        var (_, value) = await _value.EvaluateRawAsync(context);
        context.SetParameter(name, ValueTypes.ToText(value));
    }
}
