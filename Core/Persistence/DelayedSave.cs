using System;
using Avalonia.Threading;

namespace StreamAssist.Core.Persistence;



// a save that waits for things to settle (typing, dragging a splitter change a model many times a second): each Request
// starts the wait again, the save runs once it's been quiet that long. Disposing it (the app closing) writes a waiting
// save straight away instead of dropping it
public sealed class DelayedSave(Action save, TimeSpan delay) : IDisposable
{
    private IDisposable? _pending;



    public bool IsPending => _pending != null;



    public void Request()
    {
        _pending?.Dispose();
        _pending = DispatcherTimer.RunOnce(Now, delay);
    }



    public void Now()
    {
        _pending?.Dispose();
        _pending = null;
        save();
    }



    public void Dispose()
    {
        if (_pending != null) Now();
    }
}
