using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Audio.Background;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Audio.Actions;



public class SetVoicemeeterGainAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Set Voicemeeter Gain";
    [JsonIgnore] public override string Description => "Sets a channel's fader in dB (an input like Stereo Input 1, or an output bus like B1), kept to Voicemeeter's -60 to 12.";



    // private



    private readonly MenuSlot _channel;
    private readonly MenuSlot _mode;
    private readonly ValueSlot<double> _gain;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetChannelMenuItems() => VoicemeeterChannels.Names(MuteVoicemeeterAction.ChannelsNow());

    // Set to the value, or Change by it (added to what it is now, e.g. -2 for a step down)
    public static List<string> GetModeMenuItems() => ["Set to", "Change by"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("channel")]
    private ValueSlotData JsonChannel
    {
        get => _channel.GetData();
        set => _channel.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("mode")]
    private ValueSlotData JsonMode
    {
        get => _mode.GetData();
        set => _mode.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("gain")]
    private ValueSlotData JsonGain
    {
        get => _gain.GetData();
        set => _gain.LoadData(value);
    }



    // constructor



    public SetVoicemeeterGainAction()
    {
        _channel = new("Channel", GetChannelMenuItems, "Stereo Input 1", isFixed: false);
        _mode = new("Mode", GetModeMenuItems, "Set to");
        _gain = new("Gain dB");
        _slots = [_channel, _mode, _gain];
        _gain.SetText("0");

        TrackSlots();
    }



    // execute



    // Voicemeeter logs its own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var channel = await _channel.EvaluateAsync(context);
        var mode = await _mode.EvaluateAsync(context);
        var voicemeeter = GlobalContext.VoicemeeterManager ?? throw Fail("voicemeeter not set up");
        var gain = await _gain.EvaluateAsync(context);

        // a failure is already logged by the manager
        if (mode == "Change by")
        {
            if (!voicemeeter.TryGetGain(channel, out var now)) throw new BlockFailedException("gain not read");
            gain += now;
        }

        if (!voicemeeter.TrySetGain(channel, (float)gain)) throw new BlockFailedException("gain not set");
    }
}
