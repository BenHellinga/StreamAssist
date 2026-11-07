using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using StreamAssist.Components.Dashboard.Model;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Core.Persistence;

namespace StreamAssist.Components.Audio.Background;



public static class AudioHelper
{
    // const



    public const string DefaultDevice = "Default";



    // private



    private static readonly MMDeviceEnumerator DeviceEnumerator = new();



    // helpers



    // plays a sound straight away, over anything else playing, with no way to stop it (a sound with no dock). path is
    // inside Assets/ (e.g. "Sounds/boom.mp3", see AppPaths), never anywhere else on the PC
    public static bool TryPlaySound(string path, float volume = 1.0f, string? deviceName = null)
    {
        if (TryOpenSound(path, volume, deviceName) is not { } playback) return false;

        playback.Start(() => { });
        return true;
    }



    // a sound ready to play (not started), e.g. for a sound dock's queue; null if it can't be (logged). A folder gives
    // one of its sounds at random (e.g. Sounds/hype for variety)
    public static SoundPlayback? TryOpenSound(string path, float volume = 1.0f, string? deviceName = null)
    {
        if (!AppPaths.TryResolveAsset(path, out var fullPath, out var error))
        {
            AppLog.WriteError($"can't play '{path}' ({error})");
            return null;
        }

        if (System.IO.Directory.Exists(fullPath))
        {
            var sounds = System.IO.Directory.GetFiles(fullPath).Where(IsSound).ToArray();
            if (sounds.Length == 0)
            {
                AppLog.WriteError($"can't play '{path}' (no sounds in that folder)");
                return null;
            }
            fullPath = sounds[Random.Shared.Next(sounds.Length)];
        }

        if (!TryGetDeviceByString(deviceName, out var device)) return null;

        AudioFileReader audioFile;
        try
        {
            audioFile = new AudioFileReader(fullPath) { Volume = volume };
        }
        catch (Exception)
        {
            AppLog.WriteError($"can't load sound '{path}'");
            return null;
        }

        return new SoundPlayback(device, audioFile, audioFile);
    }



    // audio already in memory (e.g. Speak Text's speech), ready to play on a device (null if the device isn't there,
    // logged). It owns the stream from here and disposes it
    public static SoundPlayback? TryOpenWave(WaveStream wave, float volume, string? deviceName)
    {
        if (!TryGetDeviceByString(deviceName, out var device))
        {
            wave.Dispose();
            return null;
        }

        return new SoundPlayback(device, new VolumeSampleProvider(wave.ToSampleProvider()) { Volume = volume }.ToWaveProvider(), wave);
    }



    private static bool IsSound(string file) =>
        new[] { ".mp3", ".wav", ".ogg", ".flac", ".m4a", ".aac", ".wma" }.Contains(System.IO.Path.GetExtension(file).ToLowerInvariant());



    public static List<string> GetDeviceNames()
    {
        return DeviceEnumerator
            .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .Select(d => d.FriendlyName)
            .ToList();
    }



    public static bool TryGetDeviceByString(string? deviceName, out MMDevice device)
    {
        if (string.IsNullOrWhiteSpace(deviceName) || deviceName == DefaultDevice)
        {
            device = DeviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);;
            return true;
        }

        var match = DeviceEnumerator
            .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .FirstOrDefault(d => d.FriendlyName.Contains(deviceName, StringComparison.OrdinalIgnoreCase));

        if (match == null)
        {
            AppLog.WriteError($"audio device '{deviceName}' not found");
            device = null!;
            return false;
        }

        device = match;
        return true;
    }
}



// one sound on its device, made ready by AudioHelper: Start plays it (finished comes on the UI thread when it has played
// or was stopped), Stop ends it early. Either way its device and stream are freed once, at the end
public sealed class SoundPlayback : ISoundPlayback
{
    private readonly WasapiOut _output;
    private readonly IDisposable _source;
    private Action? _finished;
    private bool _started;
    private bool _freed;



    internal SoundPlayback(MMDevice device, IWaveProvider provider, IDisposable source)
    {
        _output = new WasapiOut(device, AudioClientShareMode.Shared, true, 100);
        _output.Init(provider);
        _source = source;
        _output.PlaybackStopped += OnStopped;
    }



    public void Start(Action finished)
    {
        if (_started || _freed) return;

        _started = true;
        _finished = finished;
        _output.Play();
    }



    public void Pause()
    {
        if (_started && !_freed) _output.Pause();
    }



    public void Resume()
    {
        if (_started && !_freed) _output.Play();
    }



    public void Stop()
    {
        if (_started) _output.Stop(); // PlaybackStopped frees it
        else Free();
    }



    private void OnStopped(object? sender, StoppedEventArgs e)
    {
        Free();
        var finished = _finished;
        if (finished != null) Avalonia.Threading.Dispatcher.UIThread.Post(finished);
    }



    private void Free()
    {
        if (_freed) return;

        _freed = true;
        _output.PlaybackStopped -= OnStopped;
        _output.Dispose();
        _source.Dispose();
    }
}
