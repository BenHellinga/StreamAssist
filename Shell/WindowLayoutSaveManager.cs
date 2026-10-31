using System;
using Avalonia.Threading;
using StreamAssist.Core.Persistence;

namespace StreamAssist.Shell;



// saves the window layout shortly after it changes, and once more when the app closes
public class WindowLayoutSaveManager : IDisposable
{
    // private



    private const string FileName = "window";

    private readonly WindowLayoutModel _model;
    private IDisposable? _pendingSave;



    // constructor



    public WindowLayoutSaveManager(WindowLayoutModel model)
    {
        _model = model;
        _pendingSave = null;

        _model.Changed += OnLayoutChanged;
    }



    // model callbacks



    // dragging a splitter changes the layout many times a second, so wait for it to settle
    private void OnLayoutChanged()
    {
        _pendingSave?.Dispose();
        _pendingSave = DispatcherTimer.RunOnce(SaveNow, TimeSpan.FromMilliseconds(500));
    }



    // public api



    public static WindowLayoutModel Load() => JsonFileStore.LoadOrReset<WindowLayoutModel>(FileName);



    public void SaveNow()
    {
        _pendingSave?.Dispose();
        _pendingSave = null;

        JsonFileStore.Save(FileName, _model);
    }



    // disposal



    public void Dispose()
    {
        _model.Changed -= OnLayoutChanged;

        // the window is closing, write the final layout instead of dropping a pending save
        if (_pendingSave != null) SaveNow();
    }
}
