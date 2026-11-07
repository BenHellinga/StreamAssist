using System;
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



    [JsonIgnore] public override string Name => "Set Voicemeeter Mute";
    [JsonIgnore] public override string Description => "Mutes, unmutes or toggles a Voicemeeter channel (an input like Stereo Input 1, or an output bus like B1), with an optional sound when it changes.";



    // private



    private readonly MenuSlot _setType;
    private readonly MenuSlot _channel;
    private string _muteSound;
    private string _unmuteSound;
    private readonly ValueSlot<double> _soundVolume;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



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

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("setType")]
    private ValueSlotData JsonSetType
    {
        get => _setType.GetData();
        set => _setType.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("channel")]
    private ValueSlotData JsonChannel
    {
        get => _channel.GetData();
        set => _channel.LoadData(value);
    }

    // how loud the mute / unmute sounds play (0 to 1, kept to that range), typed or from a block
    [JsonInclude] [JsonPropertyName("soundVolume")]
    private ValueSlotData JsonSoundVolume
    {
        get => _soundVolume.GetData();
        set => _soundVolume.LoadData(value);
    }



    // constructor



    public MuteVoicemeeterAction()
    {
        _setType = new("Set Type", GetSetTypeMenuItems, "Toggle");
        _channel = new("Channel", GetChannelMenuItems, "Stereo Input 1", isFixed: false);
        _muteSound = "";
        _soundVolume = new("Sound Volume");
        _unmuteSound = "";
        _slots = [_setType, _channel, _soundVolume];

        _soundVolume.SetText("1");

        TrackSlots();
    }



    // public



    public static List<string> GetSetTypeMenuItems() => ["Mute", "Unmute", "Toggle"];
    public static List<string> GetChannelMenuItems() => VoicemeeterChannels.Names(ChannelsNow());



    // the connected Voicemeeter's channels, Banana's until one connects
    public static IReadOnlyList<VoicemeeterChannel> ChannelsNow() =>
        GlobalContext.VoicemeeterManager?.Channels ?? VoicemeeterChannels.For(VoicemeeterChannels.Banana);



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var setType = await _setType.EvaluateAsync(context);
        var channel = await _channel.EvaluateAsync(context);
        var voicemeeter = GlobalContext.VoicemeeterManager ?? throw Fail("voicemeeter not set up");

        // one read of what Voicemeeter has now decides both the new state and the sound, so they always agree
        // (e.g. after unmuting in Voicemeeter's own window, a toggle mutes and plays the mute sound)
        if (!voicemeeter.TryGetMute(channel, out var muted)) throw new BlockFailedException("mute not read");

        var newState = setType switch
        {
            "Mute" => true,
            "Unmute" => false,
            _ => !muted,
        };

        if (!voicemeeter.TrySetMute(channel, newState)) throw new BlockFailedException("mute not set");

        var sound = newState ? _muteSound : _unmuteSound;
        if (newState == muted || sound.Trim() == "") return;

        var volume = await _soundVolume.EvaluateAsync(context);
        if (double.IsNaN(volume)) throw Fail("sound volume isn't a number");

        AudioHelper.TryPlaySound(sound, (float)Math.Clamp(volume, 0, 1));
    }
}
