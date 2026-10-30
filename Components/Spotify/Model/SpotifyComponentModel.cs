using System;
using System.Text.Json.Serialization;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Spotify.Model;



// the Spotify connection settings: the Client ID of your app on developer.spotify.com and whether to connect. Connect
// on startup is saved to Config/spotify.json, the Client ID to the "spotifyApp" section of Config/auth.json (both by
// SpotifySaveManager). The login itself (tokens) is kept by SpotifyManager in the "spotifyLogin" section
public class SpotifyComponentModel : ModelBase
{
    // constants



    // where the browser comes back to after logging in, it has to be added to the app on developer.spotify.com exactly
    // like this (spotify doesn't allow "localhost", only 127.0.0.1)
    public const int LoginPort = 8974;
    public const string RedirectUri = "http://127.0.0.1:8974/callback";



    // private



    private string _clientId;
    private bool _connectOnStartup;
    private bool _isEnabled;



    // public



    [JsonIgnore] public string ClientId => _clientId; // saved to auth.json, never to spotify.json
    [JsonIgnore] public bool ConnectOnStartup => _connectOnStartup; // saved
    [JsonIgnore] public bool IsEnabled => _isEnabled; // this session only (starts as the setting above)



    // json



    [JsonInclude] [JsonPropertyName("connectOnStartup")]
    private bool JsonConnectOnStartup
    {
        get => _connectOnStartup;
        set => _connectOnStartup = _isEnabled = value;
    }



    // events



    public event Action? ConnectionChanged; // the client id changed, connect/disconnect, or a reconnect was asked for
    public event Action? Changed; // a saved setting changed (client id, connect on startup)



    // constructor



    public SpotifyComponentModel()
    {
        _clientId = "";
        _connectOnStartup = false;
        _isEnabled = false;
    }



    // public api



    // the Connections tab saves its box when Connect is pressed
    public void SetClientId(string clientId)
    {
        var trimmed = clientId.Trim();
        if (trimmed == _clientId) return;

        _clientId = trimmed;

        Changed?.Invoke();
        ConnectionChanged?.Invoke();
    }



    public void SetIsEnabled(bool isEnabled)
    {
        if (isEnabled == _isEnabled) return;

        _isEnabled = isEnabled;
        ConnectionChanged?.Invoke();
    }



    // try again now (e.g. after a failed attempt), connecting if it wasn't
    public void Reconnect()
    {
        if (!_isEnabled) SetIsEnabled(true);
        else ConnectionChanged?.Invoke();
    }



    public void SetConnectOnStartup(bool connectOnStartup)
    {
        if (connectOnStartup == _connectOnStartup) return;

        _connectOnStartup = connectOnStartup;
        Changed?.Invoke();
    }



    // only for loading: the client id comes from auth.json, not spotify.json
    public void LoadClientId(string clientId) => _clientId = clientId;



    // disposal



    public override void Dispose() { }
}
