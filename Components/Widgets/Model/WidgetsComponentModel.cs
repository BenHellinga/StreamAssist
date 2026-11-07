using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Widgets.Model;



// the Widgets tab: widgets drawn on stream through their own browser sources, one config in use at a time (blocks find
// widgets by name in it, the same way they find docks). Also the widget server's settings, shown on the Connections tab:
// the port its pages are on (pick one and leave it, OBS's sources keep the address) and whether it starts with the
// app. Everything is saved to Config/widgets.json (WidgetsSaveManager)
public sealed class WidgetsComponentModel : IDisposable
{
    // constants



    public const int DefaultPort = 8976;



    // private



    private readonly ConfigSet<WidgetsConfigModel> _configs;
    private int _port;
    private bool _startOnStartup;
    private bool _isEnabled; // this session: the server runs



    // public



    [JsonIgnore] public ConfigSet<WidgetsConfigModel> Configs => _configs;
    [JsonIgnore] public WidgetsConfigModel? Config => _configs.Selected;
    [JsonIgnore] public int Port => _port;
    [JsonIgnore] public bool StartOnStartup => _startOnStartup;
    [JsonIgnore] public bool IsEnabled => _isEnabled;



    // json



    [JsonInclude] [JsonPropertyName("configs")]
    private List<WidgetsConfigModel> JsonConfigs
    {
        get => _configs.Items.ToList();
        set => _configs.LoadItems(value);
    }

    [JsonInclude] [JsonPropertyName("selectedConfig")]
    private string? JsonSelectedConfig
    {
        get => _configs.SelectedName;
        set => _configs.LoadSelected(value);
    }

    [JsonInclude] [JsonPropertyName("port")]
    private int JsonPort
    {
        get => _port;
        set => _port = IsValidPort(value) ? value : DefaultPort;
    }

    [JsonInclude] [JsonPropertyName("startOnStartup")]
    private bool JsonStartOnStartup
    {
        get => _startOnStartup;
        set => _startOnStartup = _isEnabled = value;
    }



    // events



    public event Action? Changed; // anything saved changed
    public event Action? ServerChanged; // the port changed, started / stopped, or a restart was asked for



    // constructor



    public WidgetsComponentModel()
    {
        _configs = new("widgets config", () => new WidgetsConfigModel());
        _port = DefaultPort;
        _startOnStartup = true;
        _isEnabled = true;

        _configs.Changed += OnConfigsChanged;
    }



    // public api



    // a widget of the config in use by name, which must be of this kind (null: any). The error is for the block to log
    public bool TryFindWidget(string name, WidgetKind? kind, out WidgetModel widget, out string error)
    {
        widget = null!;
        error = "";

        if (Config == null)
        {
            error = "no widgets config";
            return false;
        }

        if (Config.FindWidget(name) is not { } found)
        {
            error = $"no widget '{name.Trim()}'";
            return false;
        }

        if (kind != null && found.Kind != kind)
        {
            error = $"widget '{found.Name}' isn't a {WidgetModel.KindTitle(kind.Value).ToLowerInvariant()}";
            return false;
        }

        widget = found;
        return true;
    }



    // the names of the config in use's widgets of a kind (null: every one), for the blocks' Widget dropdowns
    public List<string> WidgetNames(WidgetKind? kind) =>
        Config?.Widgets.Where(w => kind == null || w.Kind == kind).Select(w => w.Name).ToList() ?? [];



    public bool TrySetPort(int port)
    {
        if (!IsValidPort(port))
        {
            AppLog.WriteError($"can't use port {port} for widgets, it's 1024 to 65535");
            return false;
        }

        if (port == _port) return true;

        _port = port;
        Changed?.Invoke();
        ServerChanged?.Invoke();
        return true;
    }



    public void SetIsEnabled(bool isEnabled)
    {
        if (isEnabled == _isEnabled) return;
        _isEnabled = isEnabled;
        ServerChanged?.Invoke();
    }



    // start again now (e.g. after the port was busy), starting it if it wasn't
    public void Restart()
    {
        if (!_isEnabled) SetIsEnabled(true);
        else ServerChanged?.Invoke();
    }



    public void SetStartOnStartup(bool startOnStartup)
    {
        if (startOnStartup == _startOnStartup) return;
        _startOnStartup = startOnStartup;
        Changed?.Invoke();
    }



    // helpers



    private static bool IsValidPort(int port) => port is >= 1024 and <= 65535;



    private void OnConfigsChanged() => Changed?.Invoke();



    // disposal



    public void Dispose()
    {
        _configs.Changed -= OnConfigsChanged;
        _configs.Dispose();
    }
}
