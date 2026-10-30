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



    private string _change;
    private string _input;
    private string _bus;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Do", ConfiguratorType.Menu)] [JsonPropertyName("change")]
    public string Change
    {
        get => _change;
        set => SetParameter(ref _change, value);
    }

    public static List<string> GetChangeMenuItems() => ["On", "Off", "Toggle"];

    [ConfigurableParameter("Input", ConfiguratorType.Menu)] [JsonPropertyName("input")]
    public string Input
    {
        get => _input;
        set => SetParameter(ref _input, value);
    }

    public static List<string> GetInputMenuItems() => VoicemeeterChannels.Names(MuteVoicemeeterAction.ChannelsNow(), inputs: true);

    [ConfigurableParameter("Bus", ConfiguratorType.Menu)] [JsonPropertyName("bus")]
    public string Bus
    {
        get => _bus;
        set => SetParameter(ref _bus, value);
    }

    public static List<string> GetBusMenuItems() => VoicemeeterChannels.Names(MuteVoicemeeterAction.ChannelsNow(), inputs: false);

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // constructor



    public SetVoicemeeterRouteAction()
    {
        _change = "Toggle";
        _input = "Stereo Input 1";
        _bus = "A1";
        _slots = [];

        TrackSlots();
    }



    // execute



    // Voicemeeter logs its own failure, then the run stops
    protected override Task ExecuteAsync(TriggerContext context)
    {
        var voicemeeter = GlobalContext.VoicemeeterManager ?? throw Fail("voicemeeter not set up");
        var routed = _change == "On";
        if (_change == "Toggle")
        {
            if (!voicemeeter.TryGetRoute(_input, _bus, out var current)) throw new BlockFailedException("route not read");
            routed = !current;
        }

        if (!voicemeeter.TrySetRoute(_input, _bus, routed)) throw new BlockFailedException("route not set");
        return Task.CompletedTask;
    }
}
