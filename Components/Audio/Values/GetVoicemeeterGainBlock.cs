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



    private readonly MenuSlot _channel;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetChannelMenuItems() => VoicemeeterChannels.Names(MuteVoicemeeterAction.ChannelsNow());

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("channel")]
    private ValueSlotData JsonChannel
    {
        get => _channel.GetData();
        set => _channel.LoadData(value);
    }



    // constructor



    public GetVoicemeeterGainBlock()
    {
        _channel = new("Channel", GetChannelMenuItems, "Stereo Input 1", isFixed: false);
        _slots = [_channel];

        TrackSlots();
    }



    // compute



    protected override async Task<double> ComputeAsync(TriggerContext context)
    {
        var channel = await _channel.EvaluateAsync(context);
        var voicemeeter = GlobalContext.VoicemeeterManager ?? throw Fail("voicemeeter not set up");
        if (!voicemeeter.TryGetGain(channel, out var gain)) throw new BlockFailedException("gain not read");
        return (double)gain;
    }
}
