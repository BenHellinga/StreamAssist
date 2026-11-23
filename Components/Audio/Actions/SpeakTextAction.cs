using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Audio.Background;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.CSharp.Api;

namespace StreamAssist.Components.Audio.Actions;



// text-to-speech with the voices Windows has installed or Kokoro's (KokoroVoices: free, offline neural voices, in
// Assets/Voices): the speech is made in memory on a worker thread, then played like a sound, so it can go to any
// output device at any volume
[SupportedOSPlatform("windows")] // Windows' own speech (the app is Windows-only anyway)
public class SpeakTextAction : ActionBlock
{
    // constants



    private const string DefaultVoice = SoundScriptApi.DefaultVoice;



    // info



    [JsonIgnore] public override string Name => "Speak Text";
    [JsonIgnore] public override string Description => "Reads text out loud with a Windows voice or a Kokoro voice (natural sounding, free, works offline). Speed is -10 (slowest) to 10, Volume 0 to 1. With no Dock it speaks straight away, over anything else playing; naming a sound dock queues it there, so speech plays one at a time in order and can be skipped or stopped. Wait Until Done holds the run until it has finished speaking (or was stopped).";
    [JsonIgnore] public override BlockGroup Group => BlockGroup.App;



    // private



    private readonly ValueSlot<string> _text;
    private readonly ValueSlot<double> _speed;
    private readonly ValueSlot<double> _volume;
    private readonly ValueSlot<string> _dock;
    private readonly MenuSlot _voice;
    private readonly MenuSlot _outputDevice;
    private bool _waitUntilDone;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Wait Until Done", ConfiguratorType.Boolean)] [JsonPropertyName("waitUntilDone")]
    public bool WaitUntilDone
    {
        get => _waitUntilDone;
        set => SetParameter(ref _waitUntilDone, value);
    }

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("voice")]
    private ValueSlotData JsonVoice
    {
        get => _voice.GetData();
        set => _voice.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("outputDevice")]
    private ValueSlotData JsonOutputDevice
    {
        get => _outputDevice.GetData();
        set => _outputDevice.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("text")]
    private ValueSlotData JsonText
    {
        get => _text.GetData();
        set => _text.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("speed")]
    private ValueSlotData JsonSpeed
    {
        get => _speed.GetData();
        set => _speed.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("volume")]
    private ValueSlotData JsonVolume
    {
        get => _volume.GetData();
        set => _volume.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("dock")]
    private ValueSlotData JsonDock
    {
        get => _dock.GetData();
        set => _dock.LoadData(value);
    }



    // constructor



    public SpeakTextAction()
    {
        _text = new("Text");
        _speed = new("Speed");
        _volume = new("Volume");
        _voice = new("Voice", GetVoiceMenuItems, DefaultVoice, isFixed: false);
        _outputDevice = new("Output Device", GetOutputDeviceMenuItems, AudioHelper.DefaultDevice, isFixed: false);
        _waitUntilDone = false;
        _dock = new("Dock");
        _slots = [_voice, _outputDevice, _text, _speed, _volume, _dock];

        _speed.SetText("0");
        _volume.SetText("1");

        TrackSlots();
    }



    // menus



    public static List<string> GetVoiceMenuItems() => SoundScriptApi.VoiceNames();



    public static List<string> GetOutputDeviceMenuItems() => [AudioHelper.DefaultDevice, .. AudioHelper.GetDeviceNames()];



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var voice = await _voice.EvaluateAsync(context);
        var outputDevice = await _outputDevice.EvaluateAsync(context);
        var text = await _text.EvaluateAsync(context);
        if (text.Trim() == "") return; // nothing to say

        var speed = await _speed.EvaluateAsync(context);
        var volume = await _volume.EvaluateAsync(context);
        if (double.IsNaN(speed)) throw Fail("speed isn't a number");
        var dock = await _dock.EvaluateAsync(context);

        await Api(context).Sound.SpeakAsync(text, voice, (int)Math.Round(Math.Clamp(speed, -10, 10)), volume, dock, outputDevice, _waitUntilDone);
    }
}
