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
    [JsonIgnore] public override string Description => "Reads text out loud with a Windows voice or a Kokoro voice (natural sounding, free, works offline). Speed is -10 (slowest) to 10, Volume 0 to 1. Wait Until Done holds the run until it has finished speaking.";
    [JsonIgnore] public override BlockGroup Group => BlockGroup.App;



    // private



    private readonly ValueSlot<string> _text;
    private readonly ValueSlot<double> _speed;
    private readonly ValueSlot<double> _volume;
    private string _voice;
    private string _outputDevice;
    private bool _waitUntilDone;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Voice", ConfiguratorType.Menu)] [JsonPropertyName("voice")]
    public string Voice
    {
        get => _voice;
        set => SetParameter(ref _voice, value);
    }

    [ConfigurableParameter("Output Device", ConfiguratorType.Menu)] [JsonPropertyName("outputDevice")]
    public string OutputDevice
    {
        get => _outputDevice;
        set => SetParameter(ref _outputDevice, value);
    }

    [ConfigurableParameter("Wait Until Done", ConfiguratorType.Boolean)] [JsonPropertyName("waitUntilDone")]
    public bool WaitUntilDone
    {
        get => _waitUntilDone;
        set => SetParameter(ref _waitUntilDone, value);
    }

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



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



    // constructor



    public SpeakTextAction()
    {
        _text = new("Text");
        _speed = new("Speed");
        _volume = new("Volume");
        _voice = DefaultVoice;
        _outputDevice = AudioHelper.DefaultDevice;
        _waitUntilDone = false;
        _slots = [_text, _speed, _volume];

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
        var text = await _text.EvaluateAsync(context);
        if (text.Trim() == "") return; // nothing to say

        var speed = await _speed.EvaluateAsync(context);
        var volume = await _volume.EvaluateAsync(context);
        if (double.IsNaN(speed)) throw Fail("speed isn't a number");
        if (double.IsNaN(volume)) throw Fail("volume isn't a number");

        var voice = _voice;
        var rate = (int)Math.Round(Math.Clamp(speed, -10, 10));

        // making the speech takes a moment (as long as a short sound), so off the UI thread. A Kokoro voice's speed is
        // a factor: -10 half, 0 normal, 10 twice as fast
        var (wave, error) = KokoroVoices.TryFind(voice, out var speaker)
            ? await Task.Run(() => KokoroVoices.Synthesize(text, speaker, Math.Pow(2, rate / 10.0)))
            : await Task.Run(() => Synthesize(text, voice, rate));
        if (wave == null) throw Fail(error);

        var played = AudioHelper.PlayWaveAsync(wave, (float)Math.Clamp(volume, 0, 1), _outputDevice);
        if (_waitUntilDone && !await played.WaitAsync(context.CancellationToken)) throw Fail("couldn't play the speech");
    }



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
