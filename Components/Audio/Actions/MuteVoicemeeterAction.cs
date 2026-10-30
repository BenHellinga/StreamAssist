using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Audio.Background;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Audio.Actions;



public class MuteVoicemeeterAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Mute/Unmute Voicemeeter";
    [JsonIgnore] public override string Description => "Mutes, unmutes or toggles a Voicemeeter channel (an input like Stereo Input 1, or an output bus like B1), with an optional sound when it changes.";



    // private



    private string _setType;
    private string _channel;
    private string _muteSound;
    private string _unmuteSound;



    // public



    [ConfigurableParameter("Set Type", ConfiguratorType.Menu)] [JsonPropertyName("setType")]
    public string SetType
    {
        get => _setType;
        set => SetParameter(ref _setType, value);
    }

    [ConfigurableParameter("Channel", ConfiguratorType.Menu)] [JsonPropertyName("channel")]
    public string Channel
    {
        get => _channel;
        set => SetParameter(ref _channel, value);
    }

    // played when it becomes muted / unmuted (empty = no sound)
    [ConfigurableParameter("Mute Sound", ConfiguratorType.Filepath)] [JsonPropertyName("muteSound")]
    public string MuteSound
    {
        get => _muteSound;
        set => SetParameter(ref _muteSound, value);
    }

    [ConfigurableParameter("Unmute Sound", ConfiguratorType.Filepath)] [JsonPropertyName("unmuteSound")]
    public string UnmuteSound
    {
        get => _unmuteSound;
        set => SetParameter(ref _unmuteSound, value);
    }



    // constructor



    public MuteVoicemeeterAction()
    {
        _setType = "Toggle";
        _channel = "Stereo Input 1";
        _muteSound = "";
        _unmuteSound = "";
    }



    // public



    public static List<string> GetSetTypeMenuItems() => ["Mute", "Unmute", "Toggle"];
    public static List<string> GetChannelMenuItems() => VoicemeeterChannels.Names(ChannelsNow());



    // the connected Voicemeeter's channels, Banana's until one connects
    public static IReadOnlyList<VoicemeeterChannel> ChannelsNow() =>
        GlobalContext.VoicemeeterManager?.Channels ?? VoicemeeterChannels.For(VoicemeeterChannels.Banana);



    // execute



    protected override Task ExecuteAsync(TriggerContext context)
    {
        var voicemeeter = GlobalContext.VoicemeeterManager ?? throw Fail("voicemeeter not set up");

        // one read of what Voicemeeter has now decides both the new state and the sound, so they always agree
        // (e.g. after unmuting in Voicemeeter's own window, a toggle mutes and plays the mute sound)
        if (!voicemeeter.TryGetMute(_channel, out var muted)) throw new BlockFailedException("mute not read");

        var newState = _setType switch
        {
            "Mute" => true,
            "Unmute" => false,
            _ => !muted,
        };

        if (!voicemeeter.TrySetMute(_channel, newState)) throw new BlockFailedException("mute not set");

        var sound = newState ? _muteSound : _unmuteSound;
        if (newState != muted && sound.Trim() != "") AudioHelper.TryPlaySound(sound);

        return Task.CompletedTask;
    }
}
