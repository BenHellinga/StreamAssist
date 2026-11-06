using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
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



    // path is inside Assets/ (e.g. "Sounds/boom.mp3", see AppPaths), never anywhere else on the PC
    public static bool TryPlaySound(string path, float volume = 1.0f, string? deviceName = null)
    {
        if (!AppPaths.TryResolveAsset(path, out var fullPath, out var error))
        {
            AppLog.WriteError($"can't play '{path}' ({error})");
            return false;
        }

        // a folder plays one of its sounds at random (e.g. Sounds/hype for variety)
        if (System.IO.Directory.Exists(fullPath))
        {
            var sounds = System.IO.Directory.GetFiles(fullPath).Where(IsSound).ToArray();
            if (sounds.Length == 0)
            {
                AppLog.WriteError($"can't play '{path}' (no sounds in that folder)");
                return false;
            }
            fullPath = sounds[Random.Shared.Next(sounds.Length)];
        }

        if (!TryGetDeviceByString(deviceName, out var device)) return false;

        AudioFileReader audioFile;
        try
        {
            audioFile = new AudioFileReader(fullPath) { Volume = volume };
        }
        catch (Exception)
        {
            AppLog.WriteError($"can't load sound '{path}'");
            return false;
        }

        var outputDevice = new WasapiOut(device, AudioClientShareMode.Shared, true, 100);

        outputDevice.Init(audioFile);
        outputDevice.Play();
        outputDevice.PlaybackStopped += (_, _) =>
        {
            outputDevice.Dispose();
            audioFile.Dispose();
        };

        return true;
    }



    // plays audio already in memory (e.g. Speak Text's speech) on a device, finished when it has played (or failed to
    // start, false). It owns the stream from here and disposes it
    public static Task<bool> PlayWaveAsync(WaveStream wave, float volume, string? deviceName)
    {
        if (!TryGetDeviceByString(deviceName, out var device))
        {
            wave.Dispose();
            return Task.FromResult(false);
        }

        var done = new TaskCompletionSource<bool>();
        var outputDevice = new WasapiOut(device, AudioClientShareMode.Shared, true, 100);
        outputDevice.Init(new VolumeSampleProvider(wave.ToSampleProvider()) { Volume = volume });
        outputDevice.PlaybackStopped += (_, _) =>
        {
            outputDevice.Dispose();
            wave.Dispose();
            done.TrySetResult(true);
        };
        outputDevice.Play();

        return done.Task;
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