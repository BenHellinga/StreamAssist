using System;
using System.Text.Json.Serialization;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.OBS.Model;



// the OBS connection settings: where obs-websocket listens (OBS: Tools > WebSocket Server Settings), its password, and
// whether to connect. The address and connect on startup are saved to Config/obs.json, the password to the "obs"
// section of Config/auth.json (both by ObsSaveManager)
public class ObsComponentModel : ModelBase
{
    // constants



    public const string DefaultAddress = "localhost:4455";



    // private



    private string _address;
    private string _password;
    private bool _connectOnStartup;
    private bool _isEnabled;



    // public



    [JsonIgnore] public string Address => _address; // host:port, saved
    [JsonIgnore] public string Password => _password; // saved to auth.json, never to obs.json
    [JsonIgnore] public bool ConnectOnStartup => _connectOnStartup; // saved
    [JsonIgnore] public bool IsEnabled => _isEnabled; // this session only (starts as the setting above)



    // json



    [JsonInclude] [JsonPropertyName("address")]
    private string JsonAddress
    {
        get => _address;
        set => _address = value;
    }

    [JsonInclude] [JsonPropertyName("connectOnStartup")]
    private bool JsonConnectOnStartup
    {
        get => _connectOnStartup;
        set => _connectOnStartup = _isEnabled = value;
    }



    // events



    public event Action? ConnectionChanged; // the address or password changed, connect/disconnect, or a reconnect was asked for
    public event Action? Changed; // a saved setting changed (address, password, connect on startup)



    // constructor



    public ObsComponentModel()
    {
        _address = DefaultAddress;
        _password = "";
        _connectOnStartup = false;
        _isEnabled = false;
    }



    // public api



    // both at once (the Connections tab saves its boxes when Connect is pressed), an empty address means the default
    public void SetConnectionDetails(string address, string password)
    {
        var trimmed = address.Trim() == "" ? DefaultAddress : address.Trim();
        if (trimmed == _address && password == _password) return;

        _address = trimmed;
        _password = password;

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



    // only for loading: the password comes from auth.json, not obs.json
    public void LoadPassword(string password) => _password = password;



    // disposal



    public override void Dispose() { }
}
