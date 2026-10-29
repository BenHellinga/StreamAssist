using System;
using System.Text.Json.Serialization;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Audio.Model;



// audio settings: for now just the Voicemeeter connection, saved to Config/audio.json by AudioSaveManager
public class AudioComponentModel : ModelBase
{
    // private



    private bool _voicemeeterConnectOnStartup;
    private bool _isVoicemeeterEnabled;



    // public



    [JsonIgnore] public bool VoicemeeterConnectOnStartup => _voicemeeterConnectOnStartup; // saved
    [JsonIgnore] public bool IsVoicemeeterEnabled => _isVoicemeeterEnabled; // this session only (starts as the setting above)



    // json



    [JsonInclude] [JsonPropertyName("voicemeeterConnectOnStartup")]
    private bool JsonVoicemeeterConnectOnStartup
    {
        get => _voicemeeterConnectOnStartup;
        set => _voicemeeterConnectOnStartup = _isVoicemeeterEnabled = value;
    }



    // events



    public event Action? VoicemeeterConnectionChanged; // connect/disconnect, or a reconnect was asked for
    public event Action? Changed; // a saved setting changed



    // constructor



    public AudioComponentModel()
    {
        _voicemeeterConnectOnStartup = false;
        _isVoicemeeterEnabled = false;
    }



    // public api



    public void SetIsVoicemeeterEnabled(bool isEnabled)
    {
        if (isEnabled == _isVoicemeeterEnabled) return;

        _isVoicemeeterEnabled = isEnabled;
        VoicemeeterConnectionChanged?.Invoke();
    }



    // try again now (e.g. after a failed attempt), connecting if it wasn't
    public void ReconnectVoicemeeter()
    {
        if (!_isVoicemeeterEnabled) SetIsVoicemeeterEnabled(true);
        else VoicemeeterConnectionChanged?.Invoke();
    }



    public void SetVoicemeeterConnectOnStartup(bool connectOnStartup)
    {
        if (connectOnStartup == _voicemeeterConnectOnStartup) return;

        _voicemeeterConnectOnStartup = connectOnStartup;
        Changed?.Invoke();
    }



    // disposal



    public override void Dispose() { }
}
