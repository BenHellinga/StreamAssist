using System;
using Avalonia.Threading;

namespace StreamAssist.Core.Mvvm;



// turns a view model flag on briefly (e.g. a block lighting up when it runs), Trigger can be called from any thread
public sealed class Flash : IDisposable
{
    // private



    private static readonly TimeSpan DefaultDuration = TimeSpan.FromMilliseconds(50);

    private readonly Action<bool> _set;
    private readonly TimeSpan _duration;
    private IDisposable? _timer;
    private bool _isDisposed;



    // constructor



    public Flash(Action<bool> set, TimeSpan? duration = null)
    {
        _set = set;
        _duration = duration ?? DefaultDuration;
        _timer = null;
        _isDisposed = false;
    }



    // public api



    // triggering again while lit just restarts the timer
    public void Trigger() => Dispatcher.UIThread.Post(Start);



    // helpers



    private void Start()
    {
        if (_isDisposed) return;

        _set(true);

        _timer?.Dispose();
        _timer = DispatcherTimer.RunOnce(() =>
        {
            _timer = null;
            _set(false);
        }, _duration);
    }



    // disposal



    public void Dispose()
    {
        _isDisposed = true;
        _timer?.Dispose();
        _timer = null;
    }
}
