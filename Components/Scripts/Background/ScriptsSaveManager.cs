using System;
using StreamAssist.Components.Scripts.Model;
using StreamAssist.Core.Persistence;

namespace StreamAssist.Components.Scripts.Background;



// saves the Scripts tab (configs, scripts and their bindings) to scripts.json shortly after it changes, and once more
// when the app closes
public sealed class ScriptsSaveManager : IDisposable
{
    // private



    public const string FileName = "scripts";

    private readonly ScriptsComponentModel _model;
    private readonly DelayedSave _save; // typing a name changes the model on every key, so wait for it to settle



    // constructor



    public ScriptsSaveManager(ScriptsComponentModel model)
    {
        _model = model;
        _save = new(Write, TimeSpan.FromSeconds(1));

        _model.Changed += OnModelChanged;
    }



    // model callbacks



    private void OnModelChanged() => _save.Request();



    // public api



    public static ScriptsComponentModel Load() => JsonFileStore.Load<ScriptsComponentModel>(FileName);



    private void Write() => JsonFileStore.Save(FileName, _model);



    // disposal



    public void Dispose()
    {
        _model.Changed -= OnModelChanged;
        _save.Dispose(); // the app is closing: a waiting save is written now
    }
}
