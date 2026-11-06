using System;
using Avalonia.Threading;
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
    private IDisposable? _pendingSave;



    // constructor



    public DashboardSaveManager(DashboardComponentModel model)
    {
        _model = model;
        _pendingSave = null;

        _model.Changed += OnModelChanged;
    }



    // model callbacks



    // dragging a splitter or typing a name changes the model many times, so wait for it to settle
    private void OnModelChanged()
    {
        _pendingSave?.Dispose();
        _pendingSave = DispatcherTimer.RunOnce(SaveNow, TimeSpan.FromSeconds(1));
    }



    // public api



    public static DashboardComponentModel Load() => JsonFileStore.Load<DashboardComponentModel>(FileName);



    public void SaveNow()
    {
        _pendingSave?.Dispose();
        _pendingSave = null;

        JsonFileStore.Save(FileName, _model);
    }



    // disposal



    public void Dispose()
    {
        _model.Changed -= OnModelChanged;

        // the app is closing, write the final state instead of dropping a pending save
        if (_pendingSave != null) SaveNow();
    }
}
