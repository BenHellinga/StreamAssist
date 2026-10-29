using System;
using System.Collections.Generic;
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
    private const int MIC_STRIP = 0;
    private const int MISC_BUS = 3;
    private const int OBS_BUS = 4;



    // private



    private readonly AudioComponentModel _model;
    private bool _connected;
    private Dictionary<StereoInputTarget, InputState> _states;



    private class InputState(bool muted, float gain)
    { 
        public bool Muted = muted;
        public float Gain = gain;
    }



    // public



    public enum StereoInputTarget { All, Misc, Obs }

    public ConnectionStatus Status { get; private set; }



    // events



    public event Action? StatusChanged;



    // constructor



    public VoicemeeterManager(AudioComponentModel model)
    {
        _model = model;
        _connected = false;
        _states = [];
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
        if (_connected)
        {
            AppLog.WriteError("Could not try to connect to Voicemeeter - already connected.");
            return true;
        }

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
            AppLog.WriteError($"Could not connect to Voicemeeter - failed to load VoicemeeterRemote64.dll ({ex.Message}).");
            SetStatus(ConnectionStatus.Failed);
            return false;
        }

        if (result != 0 && result != 1)
        {
            AppLog.WriteError($"Could not connect to Voicemeeter - code {result}.");
            SetStatus(ConnectionStatus.Failed);
            return false;
        }

        _connected = true;
        InitState();
        SetStatus(ConnectionStatus.Connected);
        return true;
    }



    public bool TryDisconnect()
    {
        if (!_connected)
        {
            AppLog.WriteError("Could not disconnect from Voicemeeter - not connected.");
            return true;
        }

        _ = Voicemeeter64.VBVMR_Logout();
        _connected = false;
        SetStatus(ConnectionStatus.NotConnected);
        return true;
    }



    public bool TrySetMute(StereoInputTarget target, bool mute)
    {
        if (!_connected)
        {
            AppLog.WriteError("Could not set mute - Voicemeeter not connected.");
            return false;
        }

        _ = Voicemeeter64.VBVMR_SetParameterFloat(ParamName(target, "Mute"), mute ? 1f : 0f);
        _states[target].Muted = mute;
        return true;
    }



    public bool TryToggleSetMute(StereoInputTarget target)
    {
        if (!_connected)
        {
            AppLog.WriteError("Could not toggle mute - Voicemeeter not connected.");
            return false;
        }

        var mute = !_states[target].Muted;
        _ = Voicemeeter64.VBVMR_SetParameterFloat(ParamName(target, "Mute"), mute ? 1f : 0f);
        _states[target].Muted = mute;
        return true;
    }



    public bool TryGetMute(StereoInputTarget target, out bool muted)
    {
        if (!_connected)
        {
            AppLog.WriteError("Could not get mute - Voicemeeter not connected.");
            muted = false;
            return false;
        }

        muted = _states[target].Muted;
        return true;
    }



    public bool TrySetGainDb(StereoInputTarget target, float db)
    {
        if (!_connected)
        {
            AppLog.WriteError("Could not set gain - Voicemeeter not connected.");
            return false;
        }

        var clamped = Math.Clamp(db, MIN_GAIN_DB, MAX_GAIN_DB);

        if (clamped != db)
            AppLog.WriteWarning($"Target db too {(clamped < db ? "high" : "low")} - clamped to {clamped}");

        _ = Voicemeeter64.VBVMR_SetParameterFloat(ParamName(target, "Gain"), clamped);
        _states[target].Gain = db;
        return true;
    }



    public bool TryGetGainDb(StereoInputTarget target, out float db)
    {
        if (!_connected)
        {
            AppLog.WriteError("Could not get gain - Voicemeeter not connected.");
            db = 0;
            return false;
        }

        db = _states[target].Gain;
        return true;
    }



    // helpers



    private bool InitState()
    {
        _states = new()
        {
            { StereoInputTarget.All, new(false, 0) },
            { StereoInputTarget.Misc, new(false, 0) },
            { StereoInputTarget.Obs, new(false, 0) },
        };

        if (!TrySetMute(StereoInputTarget.All, false)) return false;
        if (!TrySetMute(StereoInputTarget.Misc, false)) return false;
        if (!TrySetMute(StereoInputTarget.Obs, false)) return false;

        if (!TrySetGainDb(StereoInputTarget.All, 0)) return false;
        if (!TrySetGainDb(StereoInputTarget.Misc, 0)) return false;
        if (!TrySetGainDb(StereoInputTarget.Obs, 0)) return false;
        
        return true;
    }



    private void SetStatus(ConnectionStatus status)
    {
        if (status == Status) return;

        Status = status;
        StatusChanged?.Invoke();
    }



    private static string ParamName(StereoInputTarget target, string parameter)
    {
        return target switch
        {
            StereoInputTarget.All => $"Strip[{MIC_STRIP}].{parameter}",
            StereoInputTarget.Misc => $"Bus[{MISC_BUS}].{parameter}",
            StereoInputTarget.Obs => $"Bus[{OBS_BUS}].{parameter}",
            _ => throw new NotImplementedException(),
        };
    }



    // disposal



    public void Dispose()
    {
        _model.VoicemeeterConnectionChanged -= OnConnectionChanged;
        if (_connected) TryDisconnect();
    }
}
