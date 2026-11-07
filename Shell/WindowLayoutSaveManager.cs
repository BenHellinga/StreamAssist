using System;
using StreamAssist.Core.Persistence;

namespace StreamAssist.Shell;



// saves the window layout shortly after it changes, and once more when the app closes
public class WindowLayoutSaveManager : IDisposable
{
    // private



    private const string FileName = "window";

    private readonly WindowLayoutModel _model;
    private readonly DelayedSave _save; // dragging a splitter changes the layout many times a second, so wait for it to settle



    // constructor



    public WindowLayoutSaveManager(WindowLayoutModel model)
    {
        _model = model;
        _save = new(Write, TimeSpan.FromMilliseconds(500));

        _model.Changed += OnLayoutChanged;
    }



    // model callbacks



    private void OnLayoutChanged() => _save.Request();



    // public api



    public static WindowLayoutModel Load() => JsonFileStore.LoadOrReset<WindowLayoutModel>(FileName);



    public void SaveNow() => _save.Now();



    private void Write() => JsonFileStore.Save(FileName, _model);



    // disposal



    public void Dispose()
    {
        _model.Changed -= OnLayoutChanged;

        _save.Dispose(); // the window is closing: a waiting save is written now
    }
}
