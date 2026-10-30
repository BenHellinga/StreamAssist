using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Core.Connections;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Connections.UI;



// one connection in the Connections tab: its status box, a Connect / Disconnect / Retry button, and connect on startup
// the owner passes in how to read and change it (always through model methods) and calls Refresh when it changes
public partial class ConnectionRowViewModel : ViewModelBase
{
    // private



    private readonly string _label;
    private readonly Func<(ConnectionStatus Status, string Detail)> _getStatus;
    private readonly Action _connect; // also used to retry
    private readonly Action _disconnect;
    private readonly Func<bool> _getConnectOnStartup;
    private readonly Action<bool> _setConnectOnStartup;
    private readonly string _optionLabel;
    private readonly Func<bool>? _getOption;
    private readonly Action<bool>? _setOption;
    private ConnectionStatus _status;

    [ObservableProperty] private string _statusText;
    [ObservableProperty] private string _detail; // the status box's tooltip, e.g. "Connected to budderbot" (only shown on hover)
    [ObservableProperty] private string _buttonText;
    [ObservableProperty] private bool _isConnected; // green outline
    [ObservableProperty] private bool _isDown; // red outline: not connected or failed (only connecting stays grey)
    [ObservableProperty] private bool _connectOnStartup;
    [ObservableProperty] private bool _option; // the extra checkbox, if the row has one



    // public



    public string Label => _label;
    public string OptionLabel => _optionLabel;
    public bool HasOption => _getOption != null;



    // constructor



    public ConnectionRowViewModel(string label, Func<(ConnectionStatus, string)> getStatus, Action connect, Action disconnect,
        Func<bool> getConnectOnStartup, Action<bool> setConnectOnStartup,
        string optionLabel = "", Func<bool>? getOption = null, Action<bool>? setOption = null)
    {
        _label = label;
        _getStatus = getStatus;
        _connect = connect;
        _disconnect = disconnect;
        _getConnectOnStartup = getConnectOnStartup;
        _setConnectOnStartup = setConnectOnStartup;
        _optionLabel = optionLabel;
        _getOption = getOption;
        _setOption = setOption;

        _status = ConnectionStatus.NotConnected;
        _statusText = "";
        _detail = "";
        _buttonText = "";
        _connectOnStartup = false;
        _option = false;

        Refresh();
    }



    // public api



    // re-reads the status and setting (call when the model or manager says they changed)
    public void Refresh()
    {
        var (status, detail) = _getStatus();
        _status = status;

        StatusText = status switch
        {
            ConnectionStatus.Connected => "Connected",
            ConnectionStatus.Connecting => "Connecting",
            ConnectionStatus.Failed => "Failed",
            _ => "Not connected",
        };

        // connecting can be stopped too (e.g. waiting for a login code that won't come)
        ButtonText = status switch
        {
            ConnectionStatus.Connected or ConnectionStatus.Connecting => "Disconnect",
            ConnectionStatus.Failed => "Retry",
            _ => "Connect",
        };

        Detail = detail;
        IsConnected = status == ConnectionStatus.Connected;
        IsDown = status is ConnectionStatus.Failed or ConnectionStatus.NotConnected;
        ConnectOnStartup = _getConnectOnStartup();
        if (_getOption != null) Option = _getOption();
    }



    // ui events



    [RelayCommand] private void OnButtonPressed()
    {
        if (_status is ConnectionStatus.Connected or ConnectionStatus.Connecting) _disconnect();
        else _connect();
    }



    partial void OnConnectOnStartupChanged(bool value) => _setConnectOnStartup(value);



    partial void OnOptionChanged(bool value) => _setOption?.Invoke(value);



    // disposal



    // the owner subscribes to the model and manager, nothing to undo here
    public override void Dispose() { }
}
