using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Core.Utilities;

namespace StreamAssist.Components.Scripts.Model;



// one config of the Scripts tab: its scripts, top to bottom. Like a button dock's rows there's always exactly one empty
// row at the end (typing a name in it adds the next), and a row goes once its name is empty and it has no binding.
// Run Script finds a script by name (case ignored, the first one with that name)
public sealed class ScriptsConfigModel : INamedConfig
{
    // private



    private string _name;
    private readonly GrowingList<ScriptModel> _scripts;



    // public



    [JsonIgnore] public string Name => _name;
    [JsonIgnore] public IReadOnlyList<ScriptModel> Scripts => _scripts.Items; // the empty last row included



    // json



    [JsonInclude] [JsonPropertyName("name")]
    private string JsonName
    {
        get => _name;
        set => _name = value;
    }

    // the trailing empty row isn't saved, it's added back on load
    [JsonInclude] [JsonPropertyName("scripts")]
    private List<ScriptData> JsonScripts
    {
        get => _scripts.NonEmptyItems.Select(s => s.GetData()).ToList();
        set => _scripts.Load(value.Select(data =>
        {
            var script = new ScriptModel();
            script.LoadData(data);
            return script;
        }));
    }



    // events



    public event Action<ScriptModel, int>? ScriptInserted
    {
        add => _scripts.ItemInserted += value;
        remove => _scripts.ItemInserted -= value;
    }

    public event Action<ScriptModel>? ScriptRemoved
    {
        add => _scripts.ItemRemoved += value;
        remove => _scripts.ItemRemoved -= value;
    }

    public event Action<ScriptModel, int, int>? ScriptMoved // the script, where it was, where it is now
    {
        add => _scripts.ItemMoved += value;
        remove => _scripts.ItemMoved -= value;
    }

    public event Action? Changed; // a name, binding or row changed (saved)



    // constructor



    public ScriptsConfigModel()
    {
        _name = "";
        _scripts = new(() => new ScriptModel(), s => s.IsEmpty, SubscribeToScript, UnsubscribeFromScript);
        _scripts.Changed += OnScriptsChanged;
    }



    // public api



    public void SetName(string name) => _name = name;



    public ScriptModel? FindScript(string name) =>
        _scripts.NonEmptyItems.FirstOrDefault(s => s.Name.Trim() != "" && string.Equals(s.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));



    public List<string> ScriptNames() => _scripts.NonEmptyItems.Where(s => s.Name.Trim() != "").Select(s => s.Name.Trim()).ToList();



    // moves a filled script to just before the one now at index (dragged there to reorder); the empty one can't move
    public bool TryMoveScript(ScriptModel script, int index)
    {
        if (!_scripts.Items.Contains(script))
        {
            AppLog.WriteError("can't move script, not in this config");
            return false;
        }

        if (!_scripts.Move(script, index))
        {
            AppLog.WriteError("can't move an empty script");
            return false;
        }

        return true;
    }



    // model callbacks



    private void OnScriptsChanged() => Changed?.Invoke();



    private void OnScriptChanged() => Changed?.Invoke();



    private void OnScriptContentsChanged() => _scripts.Normalize();



    // helpers



    private void SubscribeToScript(ScriptModel script)
    {
        script.Changed += OnScriptChanged;
        script.NameChanged += OnScriptContentsChanged;
        script.BindableChanged += OnScriptContentsChanged;
    }



    private void UnsubscribeFromScript(ScriptModel script)
    {
        script.Changed -= OnScriptChanged;
        script.NameChanged -= OnScriptContentsChanged;
        script.BindableChanged -= OnScriptContentsChanged;
    }



    // disposal



    public void Dispose()
    {
        _scripts.Changed -= OnScriptsChanged;
        _scripts.Dispose();
    }
}
