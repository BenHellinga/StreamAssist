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



    private string _channel;
    private readonly ValueSlot<double> _gain;
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



    // json



    [JsonInclude] [JsonPropertyName("gain")]
    private ValueSlotData JsonGain
    {
        get => _gain.GetData();
        set => _gain.LoadData(value);
    }



    // constructor



    public SetVoicemeeterGainAction()
    {
        _channel = "Stereo Input 1";
        _gain = new("Gain dB");
        _slots = [_gain];
        _gain.SetText("0");

        TrackSlots();
    }



    // execute



    // Voicemeeter logs its own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var voicemeeter = GlobalContext.VoicemeeterManager ?? throw Fail("voicemeeter not set up");
        var gain = await _gain.EvaluateAsync(context);

        // a failure is already logged by the manager
        if (!voicemeeter.TrySetGain(_channel, (float)gain)) throw new BlockFailedException("gain not set");
    }
}
