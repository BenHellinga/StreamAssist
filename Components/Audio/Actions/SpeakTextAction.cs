using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Speech.Synthesis;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using NAudio.Wave;
using StreamAssist.Components.Audio.Background;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Dashboard.Model;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Audio.Actions;



// text-to-speech with the voices Windows has installed or Kokoro's (KokoroVoices: free, offline neural voices, in
// Assets/Voices): the speech is made in memory on a worker thread, then played like a sound, so it can go to any
// output device at any volume
[SupportedOSPlatform("windows")] // Windows' own speech (the app is Windows-only anyway)
public class SpeakTextAction : ActionBlock
{
    // constants



    private const string DefaultVoice = "Default";



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



    public static List<string> GetVoiceMenuItems()
    {
        try
        {
            using var synth = new SpeechSynthesizer();
            return [DefaultVoice, .. synth.GetInstalledVoices().Where(v => v.Enabled).Select(v => v.VoiceInfo.Name), .. KokoroVoices.VoiceNames];
        }
        catch (Exception)
        {
            return [DefaultVoice, .. KokoroVoices.VoiceNames]; // no Windows speech on this PC
        }
    }



    public static List<string> GetOutputDeviceMenuItems() => [AudioHelper.DefaultDevice, .. AudioHelper.GetDeviceNames()];



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var voiceOption = await _voice.EvaluateAsync(context);
        var outputDevice = await _outputDevice.EvaluateAsync(context);
        var text = await _text.EvaluateAsync(context);
        if (text.Trim() == "") return; // nothing to say

        var speed = await _speed.EvaluateAsync(context);
        var volume = await _volume.EvaluateAsync(context);
        if (double.IsNaN(speed)) throw Fail("speed isn't a number");
        if (double.IsNaN(volume)) throw Fail("volume isn't a number");

        var voice = voiceOption;
        var rate = (int)Math.Round(Math.Clamp(speed, -10, 10));
        var device = outputDevice;

        // a named sound dock is found first, so the speech takes its place in the queue now, before it's made
        var dockName = await _dock.EvaluateAsync(context);
        SoundsModel? queue = null;
        if (dockName.Trim() != "" && !(GlobalContext.DashboardComponentModel ?? throw Fail("no dashboard")).TryFindSoundQueue(dockName, out queue, out var dockError))
            throw Fail(dockError);

        // making the speech takes a moment (as long as a short sound), so off the UI thread. A Kokoro voice's speed is
        // a factor: -10 half, 0 normal, 10 twice as fast
        var made = Task.Run(() => KokoroVoices.TryFind(voice, out var speaker)
            ? KokoroVoices.Synthesize(text, speaker, Math.Pow(2, rate / 10.0))
            : Synthesize(text, voice, rate));

        if (queue != null)
        {
            var queued = queue.Enqueue(Label(text), OpenWhenMadeAsync(made, (float)Math.Clamp(volume, 0, 1), device));
            if (_waitUntilDone && queued != null) await queued.Done.WaitAsync(context.CancellationToken);
            return;
        }

        var (wave, error) = await made;
        if (wave == null) throw Fail(error);
        if (AudioHelper.TryOpenWave(wave, (float)Math.Clamp(volume, 0, 1), device) is not { } playback) throw Fail("couldn't play the speech");

        var played = new TaskCompletionSource();
        playback.Start(() => played.TrySetResult());
        if (_waitUntilDone) await played.Task.WaitAsync(context.CancellationToken);
    }



    // the speech, ready to play once it's made (null if it couldn't be: that's logged here, the queue moves on)
    private async Task<ISoundPlayback?> OpenWhenMadeAsync(Task<(WaveStream? Wave, string Error)> made, float volume, string device)
    {
        var (wave, error) = await made;
        if (wave == null)
        {
            AppLog.WriteError($"{Name}: {error}");
            return null;
        }

        return AudioHelper.TryOpenWave(wave, volume, device);
    }



    // how it shows on a sound dock: what it says on one line (the dock cuts it to its width)
    private static string Label(string text) => text.Trim().ReplaceLineEndings(" ");



    // helpers



    private static (WaveStream? Wave, string Error) Synthesize(string text, string voice, int rate)
    {
        try
        {
            using var synth = new SpeechSynthesizer();
            if (voice != DefaultVoice)
            {
                if (!synth.GetInstalledVoices().Any(v => v.Enabled && v.VoiceInfo.Name == voice)) return (null, $"no voice '{voice}' on this PC");
                synth.SelectVoice(voice);
            }

            synth.Rate = rate;

            var memory = new MemoryStream();
            synth.SetOutputToWaveStream(memory);
            synth.Speak(text);
            synth.SetOutputToNull();

            memory.Position = 0;
            return (new WaveFileReader(memory), "");
        }
        catch (Exception ex)
        {
            return (null, $"speech failed ({ex.Message})");
        }
    }
}
