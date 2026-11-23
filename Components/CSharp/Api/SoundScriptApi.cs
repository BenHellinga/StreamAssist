using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using NAudio.Wave;
using StreamAssist.Components.Audio.Background;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Dashboard.Model;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Scripting;
using StreamAssist.Shell;
using StreamAssist.Core.Utilities;

namespace StreamAssist.Components.CSharp.Api;



// ISound: sounds and speech, straight away or queued on a sound dock
[System.Runtime.Versioning.SupportedOSPlatform("windows")] // Windows' own speech (the app is Windows-only anyway)
public sealed class SoundScriptApi(TriggerContext context) : ScriptApiPart(context), ISound
{
    public const string DefaultVoice = "Default";



    public Task PlayAsync(string file, double volume = 1, string? dock = null, string? device = null, bool waitUntilDone = false) => UiThread.RunAsync(async () =>
    {
        if (double.IsNaN(volume)) throw Error("volume isn't a number");
        var queue = FindQueue(dock); // found first, so a missing dock is an error before anything plays

        // couldn't be opened: already logged
        if (AudioHelper.TryOpenSound(file, (float)Math.Clamp(volume, 0, 1), device ?? AudioHelper.DefaultDevice) is not { } playback) return;

        if (queue != null)
        {
            var queued = queue.Enqueue(Path.GetFileNameWithoutExtension(file.TrimEnd('/', '\\')), Task.FromResult<ISoundPlayback?>(playback));
            if (waitUntilDone && queued != null) await queued.Done.WaitAsync(Token);
            return;
        }

        var played = new TaskCompletionSource();
        playback.Start(() => played.TrySetResult());
        if (waitUntilDone) await played.Task.WaitAsync(Token);
    });



    public Task SpeakAsync(string text, string? voice = null, int speed = 0, double volume = 1, string? dock = null, string? device = null, bool waitUntilDone = false) => UiThread.RunAsync(async () =>
    {
        if (text.Trim() == "") return; // nothing to say
        if (double.IsNaN(volume)) throw Error("volume isn't a number");

        var rate = Math.Clamp(speed, -10, 10);
        var level = (float)Math.Clamp(volume, 0, 1);
        var picked = string.IsNullOrWhiteSpace(voice) ? DefaultVoice : voice;
        var output = device ?? AudioHelper.DefaultDevice;

        // a named sound dock is found first, so the speech takes its place in the queue now, before it's made
        var queue = FindQueue(dock);

        // making the speech takes a moment (as long as a short sound), so off the UI thread. A Kokoro voice's speed is
        // a factor: -10 half, 0 normal, 10 twice as fast
        var made = Task.Run(() => KokoroVoices.TryFind(picked, out var speaker)
            ? KokoroVoices.Synthesize(text, speaker, Math.Pow(2, rate / 10.0))
            : Synthesize(text, picked, rate));

        if (queue != null)
        {
            var queued = queue.Enqueue(text.Trim().ReplaceLineEndings(" "), OpenWhenMadeAsync(made, level, output));
            if (waitUntilDone && queued != null) await queued.Done.WaitAsync(Token);
            return;
        }

        var (wave, error) = await made;
        if (wave == null) throw Error(error);
        if (AudioHelper.TryOpenWave(wave, level, output) is not { } playback) throw Error("couldn't play the speech");

        var played = new TaskCompletionSource();
        playback.Start(() => played.TrySetResult());
        if (waitUntilDone) await played.Task.WaitAsync(Token);
    });



    public IReadOnlyList<string> Voices => UiThread.Run<IReadOnlyList<string>>(() => VoiceNames());

    public IReadOnlyList<string> OutputDevices => UiThread.Run<IReadOnlyList<string>>(() => [AudioHelper.DefaultDevice, .. AudioHelper.GetDeviceNames()]);



    // the Windows voices installed and enabled, then the Kokoro ones
    public static List<string> VoiceNames()
    {
        try
        {
            using var synth = new SpeechSynthesizer();
            return [DefaultVoice, .. synth.GetInstalledVoices().Where(v => v.Enabled).Select(v => v.VoiceInfo.Name), .. KokoroVoices.VoiceNames];
        }
        catch (Exception)
        {
            return [DefaultVoice, .. KokoroVoices.VoiceNames];
        }
    }



    // helpers



    // a named sound dock's queue, null for no dock
    private static SoundsModel? FindQueue(string? dock)
    {
        if (string.IsNullOrWhiteSpace(dock)) return null;
        var dashboard = Need(GlobalContext.DashboardComponentModel, "dashboard");
        return dashboard.TryFindSoundQueue(dock, out var queue, out var error) ? queue : throw Error(error);
    }



    // queued speech: opened once it's made (a failure is logged, and its place in the queue is skipped)
    private static async Task<ISoundPlayback?> OpenWhenMadeAsync(Task<(WaveStream? Wave, string Error)> made, float volume, string device)
    {
        var (wave, error) = await made;
        if (wave == null)
        {
            AppLog.WriteError($"can't speak text ({error})");
            return null;
        }

        return AudioHelper.TryOpenWave(wave, volume, device);
    }



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
