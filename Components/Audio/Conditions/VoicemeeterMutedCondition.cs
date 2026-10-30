using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Audio.Actions;
using StreamAssist.Components.Audio.Background;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Audio.Conditions;



public class VoicemeeterMutedCondition : ConditionBlock
{
    // info



    [JsonIgnore] public override string Name => "Voicemeeter Muted";
    [JsonIgnore] public override string Description => "True if a channel is muted in Voicemeeter (an input like Stereo Input 1, or an output bus like B1).";



    // private



    private string _channel;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Channel", ConfiguratorType.Menu)] [JsonPropertyName("channel")]
    public string Channel
    {
        get => _channel;
        set => SetParameter(ref _channel, value);
    }

    public static List<string> GetChannelMenuItems() => VoicemeeterChannels.Names(MuteVoicemeeterAction.ChannelsNow());

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // constructor



    public VoicemeeterMutedCondition()
    {
        _channel = "Stereo Input 1";
        _slots = [];

        TrackSlots();
    }



    // check



    protected override Task<bool> CheckAsync(TriggerContext context)
    {
        var voicemeeter = GlobalContext.VoicemeeterManager ?? throw Fail("voicemeeter not set up");
        if (!voicemeeter.TryGetMute(_channel, out var muted)) throw new BlockFailedException("mute not read");
        return Task.FromResult(muted);
    }
}
