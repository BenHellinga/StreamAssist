using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Scripts.Model;



// the Scripts tab: named scripts that Run Script blocks call, one config in use at a time (Run Script finds a script
// by name in it). Saved to Config/scripts.json (ScriptsSaveManager)
public sealed class ScriptsComponentModel : IDisposable
{
    // private



    private readonly ConfigSet<ScriptsConfigModel> _configs;



    // public



    [JsonIgnore] public ConfigSet<ScriptsConfigModel> Configs => _configs;
    [JsonIgnore] public ScriptsConfigModel? Config => _configs.Selected;



    // json



    [JsonInclude] [JsonPropertyName("configs")]
    private List<ScriptsConfigModel> JsonConfigs
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



    // events



    public event Action? Changed; // anything saved changed



    // constructor



    public ScriptsComponentModel()
    {
        _configs = new("scripts config", () => new ScriptsConfigModel());
        _configs.Changed += OnConfigsChanged;
    }



    // public api



    // a script of the config in use by name (the error is for the block to log)
    public bool TryFindScript(string name, out ScriptModel script, out string error)
    {
        script = null!;
        error = "";

        if (Config == null)
        {
            error = "no scripts config";
            return false;
        }

        if (Config.FindScript(name) is not { } found)
        {
            error = $"no script '{name.Trim()}'";
            return false;
        }

        script = found;
        return true;
    }



    // the config in use's script names, for Run Script's dropdown
    public List<string> ScriptNames() => Config?.ScriptNames() ?? [];



    // helpers



    private void OnConfigsChanged() => Changed?.Invoke();



    // disposal



    public void Dispose()
    {
        _configs.Changed -= OnConfigsChanged;
        _configs.Dispose();
    }
}
