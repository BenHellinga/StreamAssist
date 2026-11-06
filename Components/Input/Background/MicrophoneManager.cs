using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Threading;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Input.Model;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Input.Background;



// microphone boxes: listens to every audio input a microphone box has picked (one capture per input, shared by the boxes
// on it), works out its level (dB below full scale, the loudest bit since the last look), and for each box decides
// talking: on once the level reaches its threshold, off once it's been under it for its hold time. Talking on runs the
// box's Talking binding's start, off its stop (not while the box is off). Levels are measured on the audio thread,
// everything else on the UI thread (a timer, about 50 times a second)
public sealed class MicrophoneManager : IDisposable
{
    // constants



    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(20);
    private const int ReconcileEvery = 25; // which inputs to listen to, about twice a second
    public const double Silence = -100; // dB: nothing heard since the last look



    // private



    private sealed class Capture(WasapiCapture capture)
    {
        public WasapiCapture Device { get; } = capture;
        public readonly object Gate = new();
        public double Loudest = Silence; // since the last look, written on the audio thread
    }

    private readonly InputListModel _list;
    private readonly Dictionary<string, Capture> _captures; // by the input's id
    private readonly HashSet<string> _failed; // inputs that couldn't be opened (warned once, tried again when picked again)
    private readonly Dictionary<InputSlotModel, DateTime> _lastLoud; // when each talking box last reached its threshold
    private readonly DispatcherTimer _timer;
    private int _ticks;



    // constructor



    public MicrophoneManager(InputListModel list)
    {
        _list = list;
        _captures = new(StringComparer.OrdinalIgnoreCase);
        _failed = new(StringComparer.OrdinalIgnoreCase);
        _lastLoud = [];

        _timer = new DispatcherTimer { Interval = Tick };
        _timer.Tick += OnTick;
        _timer.Start();
    }



    // public api



    // the level of 32 bit float or 16 bit audio, as dB below full scale (its RMS: about how loud it sounds)
    public static double LevelDb(byte[] buffer, int count, WaveFormat format)
    {
        double sum = 0;
        var samples = 0;

        if (format.Encoding == WaveFormatEncoding.IeeeFloat || format.BitsPerSample == 32 && format.Encoding == WaveFormatEncoding.Extensible)
        {
            for (var i = 0; i + 4 <= count; i += 4)
            {
                var sample = BitConverter.ToSingle(buffer, i);
                sum += sample * sample;
                samples++;
            }
        }
        else if (format.BitsPerSample == 16)
        {
            for (var i = 0; i + 2 <= count; i += 2)
            {
                var sample = BitConverter.ToInt16(buffer, i) / 32768.0;
                sum += sample * sample;
                samples++;
            }
        }

        if (samples == 0 || sum <= 0) return Silence;
        return Math.Max(Silence, 10 * Math.Log10(sum / samples));
    }



    // talking: (now on, now off) for a box at this level, given whether it was talking and when it was last loud enough
    public static (bool IsTalking, bool WasLoud) Decide(bool wasTalking, double level, double threshold, TimeSpan sinceLoud, int holdMs)
    {
        if (level >= threshold) return (true, true);
        return (wasTalking && sinceLoud.TotalMilliseconds < holdMs, false);
    }



    // the timer



    private void OnTick(object? sender, EventArgs e)
    {
        if (++_ticks % ReconcileEvery == 1) Reconcile();

        var levels = _captures.ToDictionary(c => c.Key, c => TakeLoudest(c.Value), StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;

        foreach (var slot in _list.Slots.Where(s => s.Layout == InputSlotModel.MicrophoneLayout).ToList())
        {
            var level = slot.SelectedDevice is { } mic && levels.TryGetValue(mic.DevicePath, out var heard) ? heard : Silence;
            slot.SetMicLevel(level);

            var since = _lastLoud.TryGetValue(slot, out var loud) ? now - loud : TimeSpan.MaxValue;
            var (isTalking, wasLoud) = Decide(slot.IsTalking, level, slot.MicThreshold, since, slot.MicHold);
            if (wasLoud) _lastLoud[slot] = now;
            if (isTalking == slot.IsTalking) continue;

            slot.SetIsTalking(isTalking);
            if (slot.IsDisabled || slot.SelectedConfig?.Keybinds.GetValueOrDefault(InputDevices.Talking) is not { } binding) continue;

            if (isTalking) binding.TriggerStart();
            else binding.TriggerStop();
        }

        foreach (var gone in _lastLoud.Keys.Where(s => !_list.Slots.Contains(s)).ToList()) _lastLoud.Remove(gone);
    }



    // listens to the inputs microphone boxes have picked, stops listening to the rest
    private void Reconcile()
    {
        var wanted = _list.Slots.Where(s => s.Layout == InputSlotModel.MicrophoneLayout && s.SelectedDevice != null)
            .Select(s => s.SelectedDevice!).GroupBy(d => d.DevicePath, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();

        foreach (var id in _captures.Keys.Where(id => wanted.All(d => !string.Equals(d.DevicePath, id, StringComparison.OrdinalIgnoreCase))).ToList())
            Stop(id);

        _failed.RemoveWhere(id => wanted.All(d => !string.Equals(d.DevicePath, id, StringComparison.OrdinalIgnoreCase)));

        foreach (var mic in wanted.Where(d => !_captures.ContainsKey(d.DevicePath) && !_failed.Contains(d.DevicePath)))
            Start(mic);
    }



    private void Start(InputDeviceModel mic)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDevice(mic.DevicePath);
            var capture = new Capture(new WasapiCapture(device, true, 20));

            capture.Device.DataAvailable += (_, e) =>
            {
                var level = LevelDb(e.Buffer, e.BytesRecorded, capture.Device.WaveFormat);
                lock (capture.Gate) capture.Loudest = Math.Max(capture.Loudest, level);
            };
            capture.Device.RecordingStopped += (_, e) =>
            {
                if (e.Exception != null) Dispatcher.UIThread.Post(() => OnCaptureFailed(mic, e.Exception.Message));
            };

            capture.Device.StartRecording();
            _captures[mic.DevicePath] = capture;
        }
        catch (Exception ex)
        {
            OnCaptureFailed(mic, ex.Message);
        }
    }



    private void OnCaptureFailed(InputDeviceModel mic, string reason)
    {
        Stop(mic.DevicePath);
        if (_failed.Add(mic.DevicePath)) AppLog.WriteWarning($"can't listen to microphone {mic.ProductName} ({reason})");
    }



    private void Stop(string id)
    {
        if (!_captures.Remove(id, out var capture)) return;

        try
        {
            capture.Device.StopRecording();
        }
        catch (Exception)
        {
            // already stopped (e.g. unplugged)
        }

        capture.Device.Dispose();
    }



    private static double TakeLoudest(Capture capture)
    {
        lock (capture.Gate)
        {
            var loudest = capture.Loudest;
            capture.Loudest = Silence;
            return loudest;
        }
    }



    // disposal



    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        foreach (var id in _captures.Keys.ToList()) Stop(id);
    }
}



// the audio inputs Windows has right now (each a microphone device, its id what a box saves), and telling the input
// manager when one is plugged in, removed, or turned on or off
public sealed class Microphones : IMMNotificationClient, IDisposable
{
    private readonly MMDeviceEnumerator _enumerator;
    private readonly Action _changed;



    public Microphones(Action changed)
    {
        _changed = changed;
        _enumerator = new MMDeviceEnumerator();
        try
        {
            _enumerator.RegisterEndpointNotificationCallback(this);
        }
        catch (Exception)
        {
            // no notifications: the list is still read at startup
        }
    }



    // every active input, keeping the models of ones still there
    public List<InputDeviceModel> Enumerate(IReadOnlyList<InputDeviceModel> existing)
    {
        try
        {
            return _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                .Select(d => existing.FirstOrDefault(e => e.Kind == DeviceKind.Microphone && string.Equals(e.DevicePath, d.ID, StringComparison.OrdinalIgnoreCase))
                    ?? new InputDeviceModel(d.ID, d.FriendlyName, "Audio", 0, 0, DeviceKind.Microphone))
                .ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }



    // windows calls these on its own thread: the input manager reads the list again on the UI thread
    public void OnDeviceStateChanged(string deviceId, DeviceState newState) => Dispatcher.UIThread.Post(_changed);
    public void OnDeviceAdded(string pwstrDeviceId) => Dispatcher.UIThread.Post(_changed);
    public void OnDeviceRemoved(string deviceId) => Dispatcher.UIThread.Post(_changed);
    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId) { }
    public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }



    public void Dispose()
    {
        try
        {
            _enumerator.UnregisterEndpointNotificationCallback(this);
        }
        catch (Exception)
        {
            // never registered
        }

        _enumerator.Dispose();
    }
}
