using System;
using StreamAssist.Components.Widgets.Model;
using StreamAssist.Core.Persistence;

namespace StreamAssist.Components.Widgets.Background;



// saves the Widgets tab (configs, widgets and their settings, the server's port) to widgets.json shortly after it
// changes, and once more when the app closes. Levels, rotations and spins aren't saved
public sealed class WidgetsSaveManager : IDisposable
{
    // private



    public const string FileName = "widgets";

    private readonly WidgetsComponentModel _model;
    private readonly DelayedSave _save; // typing a name or dragging a value changes the model many times, so wait for it to settle



    // constructor



    public WidgetsSaveManager(WidgetsComponentModel model)
    {
        _model = model;
        _save = new(Write, TimeSpan.FromSeconds(1));

        _model.Changed += OnModelChanged;
    }



    // model callbacks



    private void OnModelChanged() => _save.Request();



    // public api



    public static WidgetsComponentModel Load() => JsonFileStore.Load<WidgetsComponentModel>(FileName);



    private void Write() => JsonFileStore.Save(FileName, _model);



    // disposal



    public void Dispose()
    {
        _model.Changed -= OnModelChanged;
        _save.Dispose(); // the app is closing: a waiting save is written now
    }
}
