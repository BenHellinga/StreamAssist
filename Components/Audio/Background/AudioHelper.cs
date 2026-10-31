using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.CoreAudioApi;
using NAudio.Wave;
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