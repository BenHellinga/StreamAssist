using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Audio.Background;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Audio.Actions;



public class SetVoicemeeterRouteAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Set Voicemeeter Route";
    [JsonIgnore] public override string Description => "Turns an input's output button on or off (sends it to that bus or stops), e.g. Stereo Input 1 to B1, or toggles it.";



    // private



    private readonly MenuSlot _change;
    private readonly MenuSlot _input;
    private readonly MenuSlot _bus;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetChangeMenuItems() => ["On", "Off", "Toggle"];


    public static List<string> GetInputMenuItems() => VoicemeeterChannels.Names(MuteVoicemeeterAction.ChannelsNow(), inputs: true);


    public static List<string> GetBusMenuItems() => VoicemeeterChannels.Names(MuteVoicemeeterAction.ChannelsNow(), inputs: false);

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("change")]
    private ValueSlotData JsonChange
    {
        get => _change.GetData();
        set => _change.LoadData(value);
    }

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



    public SetVoicemeeterRouteAction()
    {
        _change = new("Do", GetChangeMenuItems, "Toggle");
        _input = new("Input", GetInputMenuItems, "Stereo Input 1", isFixed: false);
        _bus = new("Bus", GetBusMenuItems, "A1", isFixed: false);
        _slots = [_change, _input, _bus];

        TrackSlots();
    }



    // execute



    // Voicemeeter logs its own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var change = await _change.EvaluateAsync(context);
        var input = await _input.EvaluateAsync(context);
        var bus = await _bus.EvaluateAsync(context);
        var voicemeeter = GlobalContext.VoicemeeterManager ?? throw Fail("voicemeeter not set up");
        var routed = change == "On";
        if (change == "Toggle")
        {
            if (!voicemeeter.TryGetRoute(input, bus, out var current)) throw new BlockFailedException("route not read");
            routed = !current;
        }

        if (!voicemeeter.TrySetRoute(input, bus, routed)) throw new BlockFailedException("route not set");
        return;
    }
}
