using System;
using StreamAssist.Components.Dashboard.Model;
using StreamAssist.Core.Persistence;

namespace StreamAssist.Components.Dashboard.Background;



// saves the dashboard (configs, docks, layout, button rows) to dashboard.json shortly after it changes, and once more when the
// app closes. Console lines and timers aren't saved
public class DashboardSaveManager : IDisposable
{
    // private



    public const string FileName = "dashboard";

    private readonly DashboardComponentModel _model;
    private readonly DelayedSave _save; // dragging a splitter or typing a name changes the model many times, so wait for it to settle



    // constructor



    public DashboardSaveManager(DashboardComponentModel model)
    {
        _model = model;
        _save = new(Write, TimeSpan.FromSeconds(1));

        _model.Changed += OnModelChanged;
    }



    // model callbacks



    private void OnModelChanged() => _save.Request();



    // public api



    public static DashboardComponentModel Load() => JsonFileStore.Load<DashboardComponentModel>(FileName);



    public void SaveNow() => _save.Now();



    private void Write() => JsonFileStore.Save(FileName, _model);



    // disposal



    public void Dispose()
    {
        _model.Changed -= OnModelChanged;

        _save.Dispose(); // the app is closing: a waiting save is written now
    }
}
