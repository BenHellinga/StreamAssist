using System;
using System.Collections.Generic;
using System.Linq;
using StreamAssist.Components.Audio.Model;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Core.Connections;

namespace StreamAssist.Components.Audio.Background;



// talks to Voicemeeter, connecting/disconnecting when the audio model says so (Connections tab, or connect on startup)
public sealed class VoicemeeterManager : IDisposable
{
    // constants



    private const float MIN_GAIN_DB = -60f;
    private const float MAX_GAIN_DB = 12f;



    // private



    private readonly AudioComponentModel _model;
    private bool _connected;
    private int _edition;



    // public



    public ConnectionStatus Status { get; private set; }

    // the channels of the Voicemeeter that's connected (Banana's until one is), for the blocks' dropdowns
    public IReadOnlyList<VoicemeeterChannel> Channels => VoicemeeterChannels.For(_edition);



    // events



    public event Action? StatusChanged;



    // constructor



    public VoicemeeterManager(AudioComponentModel model)
    {
        _model = model;
        _connected = false;
        _edition = VoicemeeterChannels.Banana;
        Status = ConnectionStatus.NotConnected;

        _model.VoicemeeterConnectionChanged += OnConnectionChanged;

        if (_model.IsVoicemeeterEnabled) TryConnect();
    }



    // model callbacks



    // connect, disconnect, or reconnect (a retry), whatever the model now says
    private void OnConnectionChanged()
    {
        if (_connected) TryDisconnect();
        if (_model.IsVoicemeeterEnabled) TryConnect();
        else SetStatus(ConnectionStatus.NotConnected);
    }



    // public api



    public bool TryConnect()
    {
        if (_connected) return true; // already what was asked for, nothing to say

        var paths = new List<string>
        {
            @"C:\Program Files (x86)\VB\Voicemeeter",
            @"C:\Program Files\VB\Voicemeeter"
        };

        foreach (var path in paths)
        {
            if (!System.IO.Directory.Exists(path)) continue;

            Voicemeeter64.SetDllDirectory(path);
            break;
        }

        int result;
        try
        {
            result = Voicemeeter64.VBVMR_Login();
        }
        catch (Exception ex)
        {
            AppLog.WriteError($"can't load voicemeeter dll ({ex.Message})");
            SetStatus(ConnectionStatus.Failed);
            return false;
        }

        if (result != 0 && result != 1)
        {
            AppLog.WriteError($"can't connect to voicemeeter, code {result}");
            SetStatus(ConnectionStatus.Failed);
            return false;
        }

        // nothing is changed on connect: mutes and gains stay as they are in Voicemeeter, the blocks read them from there
        var edition = 0;
        if (Voicemeeter64.VBVMR_GetVoicemeeterType(ref edition) == 0 && edition is >= 1 and <= 3) _edition = edition;
        _connected = true;
        SetStatus(ConnectionStatus.Connected);
        return true;
    }



    public bool TryDisconnect()
    {
        if (!_connected) return true; // already what was asked for, nothing to say

        _ = Voicemeeter64.VBVMR_Logout();
        _connected = false;
        SetStatus(ConnectionStatus.NotConnected);
        return true;
    }



    // channels (by the name Voicemeeter's window shows, e.g. "Stereo Input 1", "B2"), each logs why if it can't



    public bool TryGetMute(string channel, out bool muted)
    {
        muted = false;
        if (!TryFind(channel, "read mute", out var found) || !TryGetParameter($"{found.Prefix}.Mute", out var value)) return false;

        muted = value >= 0.5f;
        return true;
    }



    public bool TrySetMute(string channel, bool muted) =>
        TryFind(channel, "set mute", out var found) && TrySetParameter($"{found.Prefix}.Mute", muted ? 1 : 0);



    public bool TryGetGain(string channel, out float db)
    {
        db = 0;
        return TryFind(channel, "read gain", out var found) && TryGetParameter($"{found.Prefix}.Gain", out db);
    }



    // in dB like the fader, kept to Voicemeeter's -60 to 12
    public bool TrySetGain(string channel, float db)
    {
        if (!TryFind(channel, "set gain", out var found)) return false;

        var clamped = Math.Clamp(db, MIN_GAIN_DB, MAX_GAIN_DB);
        if (clamped != db) AppLog.WriteWarning($"gain too {(clamped < db ? "high" : "low")}, clamped to {clamped}");

        return TrySetParameter($"{found.Prefix}.Gain", clamped);
    }



    // whether an input is sent to an output bus (its A1 / B1 ... buttons)
    public bool TryGetRoute(string input, string bus, out bool routed)
    {
        routed = false;
        if (!TryRoute(input, bus, "read route", out var name) || !TryGetParameter(name, out var value)) return false;

        routed = value >= 0.5f;
        return true;
    }



    public bool TrySetRoute(string input, string bus, bool routed) =>
        TryRoute(input, bus, "set route", out var name) && TrySetParameter(name, routed ? 1 : 0);



    // any parameter by its Voicemeeter remote name, e.g. "Strip[0].Comp" or "Bus[3].EQ.on" (anything the blocks above
    // don't cover), reads what Voicemeeter has right now



    public bool TryGetParameter(string name, out float value)
    {
        value = 0;
        if (!_connected)
        {
            AppLog.WriteError($"can't read voicemeeter {name}, not connected");
            return false;
        }

        // Voicemeeter refreshes what GetParameterFloat gives when asked whether anything changed, and after a change made
        // in its own window that can take more than one ask, so ask until it says nothing more changed (a few at most)
        for (int i = 0; i < 20 && Voicemeeter64.VBVMR_IsParametersDirty() > 0; i++) { }

        var result = Voicemeeter64.VBVMR_GetParameterFloat(name, ref value);
        if (result == 0) return true;

        AppLog.WriteError($"can't read voicemeeter {name}, {(result == -3 ? "no such parameter" : $"code {result}")}");
        return false;
    }



    public bool TrySetParameter(string name, float value)
    {
        if (!_connected)
        {
            AppLog.WriteError($"can't set voicemeeter {name}, not connected");
            return false;
        }

        var result = Voicemeeter64.VBVMR_SetParameterFloat(name, value);
        if (result == 0) return true;

        AppLog.WriteError($"can't set voicemeeter {name}, {(result == -3 ? "no such parameter" : $"code {result}")}");
        return false;
    }



    // helpers



    private void SetStatus(ConnectionStatus status)
    {
        if (status == Status) return;

        Status = status;
        StatusChanged?.Invoke();
    }



    private bool TryFind(string channel, string what, out VoicemeeterChannel found)
    {
        found = Channels.FirstOrDefault(c => c.Name == channel.Trim())!;
        if (found != null) return true;

        AppLog.WriteError($"can't {what}, voicemeeter has no channel '{channel}'");
        return false;
    }



    // e.g. ("Stereo Input 1", "B1") -> "Strip[0].B1"
    private bool TryRoute(string input, string bus, string what, out string name)
    {
        name = "";
        if (!TryFind(input, what, out var from) || !TryFind(bus, what, out var to)) return false;

        if (!from.IsInput || to.IsInput)
        {
            AppLog.WriteError($"can't {what}, it goes from an input to an output bus");
            return false;
        }

        name = $"{from.Prefix}.{to.Name}";
        return true;
    }



    // disposal



    public void Dispose()
    {
        _model.VoicemeeterConnectionChanged -= OnConnectionChanged;
        if (_connected) TryDisconnect();
    }
}
