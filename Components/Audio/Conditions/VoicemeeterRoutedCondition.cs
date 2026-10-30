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



    private string _input;
    private string _bus;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



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



    public VoicemeeterRoutedCondition()
    {
        _input = "Stereo Input 1";
        _bus = "A1";
        _slots = [];

        TrackSlots();
    }



    // check



    protected override Task<bool> CheckAsync(TriggerContext context)
    {
        var voicemeeter = GlobalContext.VoicemeeterManager ?? throw Fail("voicemeeter not set up");
        if (!voicemeeter.TryGetRoute(_input, _bus, out var routed)) throw new BlockFailedException("route not read");
        return Task.FromResult(routed);
    }
}
