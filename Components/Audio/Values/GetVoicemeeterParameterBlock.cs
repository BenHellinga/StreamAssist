using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Audio.Actions;
using StreamAssist.Components.Audio.Background;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Audio.Values;



public class GetVoicemeeterParameterBlock : ValueBlock<double>
{
    // info



    [JsonIgnore] public override string Name => "Get Voicemeeter Parameter";
    [JsonIgnore] public override string Description => "Any Voicemeeter setting by its remote API name, e.g. Strip[0].Comp or Bus[3].EQ.on. Strip[0] is the first input, Bus[0] is A1.";



    // private



    private readonly ValueSlot<string> _parameter;
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



    // constructor



    public GetVoicemeeterParameterBlock()
    {
        _parameter = new("Parameter");
        _slots = [_parameter];

        TrackSlots();
    }



    // compute



    protected override async Task<double> ComputeAsync(TriggerContext context)
    {
        var voicemeeter = GlobalContext.VoicemeeterManager ?? throw Fail("voicemeeter not set up");
        var parameter = (await _parameter.EvaluateAsync(context)).Trim();
        if (parameter == "") throw Fail("needs a parameter name");

        if (!voicemeeter.TryGetParameter(parameter, out var value)) throw new BlockFailedException("parameter not read");
        return value;
    }
}
