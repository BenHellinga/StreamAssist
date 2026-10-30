using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Audio.Actions;
using StreamAssist.Components.Audio.Background;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Audio.Values;



public class GetVoicemeeterGainBlock : ValueBlock<double>
{
    // info



    [JsonIgnore] public override string Name => "Get Voicemeeter Gain";
    [JsonIgnore] public override string Description => "A channel's fader in dB (an input like Stereo Input 1, or an output bus like B1), read from Voicemeeter.";



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



    public GetVoicemeeterGainBlock()
    {
        _channel = "Stereo Input 1";
        _slots = [];

        TrackSlots();
    }



    // compute



    protected override Task<double> ComputeAsync(TriggerContext context)
    {
        var voicemeeter = GlobalContext.VoicemeeterManager ?? throw Fail("voicemeeter not set up");
        if (!voicemeeter.TryGetGain(_channel, out var gain)) throw new BlockFailedException("gain not read");
        return Task.FromResult((double)gain);
    }
}
