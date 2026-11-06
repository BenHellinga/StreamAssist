using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NAudio.Wave;
using SherpaOnnx;
using StreamAssist.Core.Persistence;

namespace StreamAssist.Components.Audio.Background;



// Kokoro: free, offline neural voices (Apache 2.0, fine to use on stream), run on the CPU through sherpa-onnx. The model
// (kokoro-multi-lang-v1_0, about 350 MB) lives in Assets/Voices; without it there are simply no Kokoro voices. It's
// loaded the first time a Kokoro voice speaks (a few seconds), then kept. Its voices are named by accent and gender
// ("Kokoro: Heart (US, female)"), as Speak Text's voice dropdown shows and saves them
public static class KokoroVoices
{
    // constants



    public const string Prefix = "Kokoro: ";
    private const string Folder = "kokoro-multi-lang-v1_0";

    // the model's speakers in its order (speaker id = index), the first letter their accent / language, the second
    // their gender
    private static readonly string[] Speakers =
    [
        "af_alloy", "af_aoede", "af_bella", "af_heart", "af_jessica", "af_kore", "af_nicole", "af_nova", "af_river", "af_sarah",
        "af_sky", "am_adam", "am_echo", "am_eric", "am_fenrir", "am_liam", "am_michael", "am_onyx", "am_puck", "am_santa",
        "bf_alice", "bf_emma", "bf_isabella", "bf_lily", "bm_daniel", "bm_fable", "bm_george", "bm_lewis", "ef_dora", "em_alex",
        "ff_siwis", "hf_alpha", "hf_beta", "hm_omega", "hm_psi", "if_sara", "im_nicola", "jf_alpha", "jf_gongitsune",
        "jf_nezumi", "jf_tebukuro", "jm_kumo", "pf_dora", "pm_alex", "pm_santa", "zf_xiaobei", "zf_xiaoni", "zf_xiaoxiao",
        "zf_xiaoyi", "zm_yunjian", "zm_yunxi", "zm_yunxia", "zm_yunyang", "em_santa",
    ];

    private static readonly Dictionary<char, string> Accents = new()
    {
        ['a'] = "US", ['b'] = "UK", ['e'] = "Spanish", ['f'] = "French", ['h'] = "Hindi", ['i'] = "Italian",
        ['j'] = "Japanese", ['p'] = "Portuguese", ['z'] = "Chinese",
    };



    // private



    private static readonly object Gate = new(); // one model, one speech made at a time
    private static OfflineTts? _tts;



    // public



    public static string ModelDir => Path.Combine(AppPaths.AssetsDir, "Voices", Folder);
    public static bool IsInstalled => File.Exists(Path.Combine(ModelDir, "model.onnx")) && File.Exists(Path.Combine(ModelDir, "voices.bin"));

    // every voice's name, as the dropdown shows it (none while the model isn't there)
    public static IReadOnlyList<string> VoiceNames => IsInstalled ? AllVoiceNames : [];
    public static IReadOnlyList<string> AllVoiceNames => Speakers.Select(DisplayName).ToList();



    // public api



    // "af_heart" -> "Kokoro: Heart (US, female)"
    public static string DisplayName(string speaker)
    {
        var accent = Accents.GetValueOrDefault(speaker[0], "?");
        var gender = speaker[1] == 'f' ? "female" : "male";
        var name = speaker[3..];
        return $"{Prefix}{char.ToUpperInvariant(name[0])}{name[1..]} ({accent}, {gender})";
    }



    // the speaker id of a voice name from the dropdown, false if it isn't a Kokoro voice
    public static bool TryFind(string voice, out int speakerId)
    {
        speakerId = Array.FindIndex(Speakers, s => DisplayName(s) == voice);
        return speakerId >= 0;
    }



    // the speech as a wave (call it off the UI thread: loading the model the first time and making the speech take a
    // moment). speed: 1 normal, 2 twice as fast, 0.5 half
    public static (WaveStream? Wave, string Error) Synthesize(string text, int speakerId, double speed)
    {
        if (!IsInstalled) return (null, $"the Kokoro voices aren't installed ({ModelDir})");

        try
        {
            lock (Gate)
            {
                _tts ??= Load();
                var audio = _tts.Generate(text, (float)speed, speakerId);

                var bytes = new byte[audio.Samples.Length * sizeof(float)];
                Buffer.BlockCopy(audio.Samples, 0, bytes, 0, bytes.Length);
                return (new RawSourceWaveStream(new MemoryStream(bytes), WaveFormat.CreateIeeeFloatWaveFormat(audio.SampleRate, 1)), "");
            }
        }
        catch (Exception ex)
        {
            return (null, $"Kokoro couldn't speak ({ex.Message})");
        }
    }



    // helpers



    private static OfflineTts Load()
    {
        var config = new OfflineTtsConfig();
        config.Model.Kokoro.Model = Path.Combine(ModelDir, "model.onnx");
        config.Model.Kokoro.Voices = Path.Combine(ModelDir, "voices.bin");
        config.Model.Kokoro.Tokens = Path.Combine(ModelDir, "tokens.txt");
        config.Model.Kokoro.DataDir = Path.Combine(ModelDir, "espeak-ng-data");
        config.Model.Kokoro.DictDir = Path.Combine(ModelDir, "dict");
        config.Model.Kokoro.Lexicon = $"{Path.Combine(ModelDir, "lexicon-us-en.txt")},{Path.Combine(ModelDir, "lexicon-zh.txt")}";
        config.Model.NumThreads = Math.Clamp(Environment.ProcessorCount / 2, 1, 4);
        config.Model.Provider = "cpu";
        config.MaxNumSentences = 1;
        return new OfflineTts(config);
    }
}
