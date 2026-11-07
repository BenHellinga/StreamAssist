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



    public VoicemeeterMutedCondition()
    {
        _channel = new("Channel", GetChannelMenuItems, "Stereo Input 1", isFixed: false);
        _slots = [_channel];

        TrackSlots();
    }



    // check



    protected override async Task<bool> CheckAsync(TriggerContext context)
    {
        var channel = await _channel.EvaluateAsync(context);
        var voicemeeter = GlobalContext.VoicemeeterManager ?? throw Fail("voicemeeter not set up");
        if (!voicemeeter.TryGetMute(channel, out var muted)) throw new BlockFailedException("mute not read");
        return muted;
    }
}
