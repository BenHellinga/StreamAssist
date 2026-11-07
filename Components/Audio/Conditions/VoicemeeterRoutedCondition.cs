using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Audio.Actions;
using StreamAssist.Components.Audio.Background;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Audio.Conditions;



public class VoicemeeterRoutedCondition : ConditionBlock
{
    // info



    [JsonIgnore] public override string Name => "Voicemeeter Routed";
    [JsonIgnore] public override string Description => "True if an input is sent to an output bus (its A1 / B1 ... button is on), e.g. Stereo Input 1 to B1.";



    // private



    private readonly MenuSlot _input;
    private readonly MenuSlot _bus;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetInputMenuItems() => VoicemeeterChannels.Names(MuteVoicemeeterAction.ChannelsNow(), inputs: true);


    public static List<string> GetBusMenuItems() => VoicemeeterChannels.Names(MuteVoicemeeterAction.ChannelsNow(), inputs: false);

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("input")]
    private ValueSlotData JsonInput
    {
        get => _input.GetData();
        set => _input.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("bus")]
    private ValueSlotData JsonBus
    {
        get => _bus.GetData();
        set => _bus.LoadData(value);
    }



    // constructor



    public VoicemeeterRoutedCondition()
    {
        _input = new("Input", GetInputMenuItems, "Stereo Input 1", isFixed: false);
        _bus = new("Bus", GetBusMenuItems, "A1", isFixed: false);
        _slots = [_input, _bus];

        TrackSlots();
    }



    // check



    protected override async Task<bool> CheckAsync(TriggerContext context)
    {
        var input = await _input.EvaluateAsync(context);
        var bus = await _bus.EvaluateAsync(context);
        var voicemeeter = GlobalContext.VoicemeeterManager ?? throw Fail("voicemeeter not set up");
        if (!voicemeeter.TryGetRoute(input, bus, out var routed)) throw new BlockFailedException("route not read");
        return routed;
    }
}
