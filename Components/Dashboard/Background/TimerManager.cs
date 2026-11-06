using System;
using Avalonia.Threading;
using StreamAssist.Components.Dashboard.Model;

namespace StreamAssist.Components.Dashboard.Background;



// ends every timer dock's timers when their time is up: checks ten times a second (the timers keep their end time, so a late
// tick never makes them drift, it only runs their end up to a tenth of a second late)
public sealed class TimerManager : IDisposable
{
    // private



    private readonly DashboardComponentModel _dashboard;
    private readonly DispatcherTimer _tick;



    // constructor



    public TimerManager(DashboardComponentModel dashboard)
    {
        _dashboard = dashboard;
        _tick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };

        _tick.Tick += OnTick;
        _tick.Start();
    }



    // callbacks



    private void OnTick(object? sender, EventArgs e) => _dashboard.FinishDueTimers();



    // disposal



    public void Dispose()
    {
        _tick.Stop();
        _tick.Tick -= OnTick;
    }
}
