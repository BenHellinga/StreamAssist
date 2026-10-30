using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Audio.Background;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Audio.Actions;



public class SetVoicemeeterParameterAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Set Voicemeeter Parameter";
    [JsonIgnore] public override string Description => "Sets any Voicemeeter setting by its remote API name, for things the other blocks don't cover, e.g. Strip[0].Comp (0 to 10) or Bus[3].EQ.on (1 / 0). Strip[0] is the first input, Bus[0] is A1.";



    // private



    private readonly ValueSlot<string> _parameter;
    private readonly ValueSlot<double> _value;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("parameter")]
    private ValueSlotData JsonParameter
    {
        get => _parameter.GetData();
        set => _parameter.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("value")]
    private ValueSlotData JsonValue
    {
        get => _value.GetData();
        set => _value.LoadData(value);
    }



    // constructor



    public SetVoicemeeterParameterAction()
    {
        _parameter = new("Parameter");
        _value = new("Value");
        _slots = [_parameter, _value];

        TrackSlots();
    }



    // execute



    // Voicemeeter logs its own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var voicemeeter = GlobalContext.VoicemeeterManager ?? throw Fail("voicemeeter not set up");
        var parameter = (await _parameter.EvaluateAsync(context)).Trim();
        if (parameter == "") throw Fail("needs a parameter name");
        var value = await _value.EvaluateAsync(context);

        if (!voicemeeter.TrySetParameter(parameter, (float)value)) throw new BlockFailedException("parameter not set");
    }
}
