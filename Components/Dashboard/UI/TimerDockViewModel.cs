using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Dashboard.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Dashboard.UI;



// a timer dock's body: its own running timers, the one ending soonest at the top (paused ones by what they
// have left). The model keeps end times, not counts, so this only redraws: a few times a second the times left are
// worked out again, and the rows put back in order if that changed
public sealed partial class TimerDockViewModel : ViewModelBase
{
    // private



    private readonly TimersModel _timers;
    private readonly DispatcherTimer _redraw;

    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _anyRunning; // any not paused: the header's pause button pauses them all, else it resumes all
    [ObservableProperty] private string _addSeconds; // the header's time box: what every ＋ adds (seconds, negative takes away)



    // public



    public ObservableCollection<TimerRowViewModel> Rows { get; }



    // constructor



    public TimerDockViewModel(TimersModel timers)
    {
        _timers = timers;
        _addSeconds = "60";
        _redraw = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        Rows = [];

        Refresh();

        _timers.TimersChanged += Refresh;
        _redraw.Tick += OnRedraw;
        _redraw.Start();
    }



    // callbacks



    private void OnRedraw(object? sender, EventArgs e) => Refresh();



    // helpers



    private void Refresh()
    {
        var now = _timers.Now;
        var order = _timers.Timers.OrderBy(t => t.LeftAt(now)).ThenBy(t => t.Name).ToList();

        if (!order.SequenceEqual(Rows.Select(r => r.Timer)))
        {
            Rows.Clear();
            foreach (var timer in order) Rows.Add(new TimerRowViewModel(_timers, timer, AddAmount));
        }

        foreach (var row in Rows) row.Update(now);
        IsEmpty = Rows.Count == 0;
        AnyRunning = Rows.Any(r => !r.IsPaused);
    }



    // header buttons: the same three as a row's, for every timer



    [RelayCommand] private void OnPauseAllPressed() => _timers.SetAllPaused(AnyRunning);



    [RelayCommand] private void OnFinishAllPressed() => _timers.FinishAll();



    [RelayCommand] private void OnCancelAllPressed() => _timers.KillAll();



    [RelayCommand] private void OnAddTimeAllPressed()
    {
        if (AddAmount() is { } amount) _timers.AddTimeAll(amount);
    }



    // the time box as an amount, null while it isn't a number (the ＋ buttons then do nothing)
    private TimeSpan? AddAmount() =>
        double.TryParse(AddSeconds.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds)
        && Math.Abs(seconds) < 1e9 ? TimeSpan.FromSeconds(seconds) : null;



    // disposal



    public override void Dispose()
    {
        _timers.TimersChanged -= Refresh;
        _redraw.Tick -= OnRedraw;
        _redraw.Stop();
    }
}



// one running timer: its name, what's left, and pause / finish now / cancel / ＋ (the header's time box)
public sealed partial class TimerRowViewModel(TimersModel timers, RunningTimer timer, Func<TimeSpan?> addAmount) : ViewModelBase
{
    [ObservableProperty] private string _left = "";
    [ObservableProperty] private bool _isPaused;



    public RunningTimer Timer => timer;
    public string Name => timer.Name;



    // "0:42", "12:05", "1:02:03" (rounded up, so it shows 0:00 only as it ends)
    public void Update(DateTime now)
    {
        var seconds = (long)Math.Ceiling(timer.LeftAt(now).TotalSeconds);
        var (h, m, s) = (seconds / 3600, seconds / 60 % 60, seconds % 60);
        Left = h > 0 ? $"{h}:{m:00}:{s:00}" : $"{m}:{s:00}";
        IsPaused = timer.IsPaused;
    }



    [RelayCommand] private void OnPausePressed() => timers.SetIsPaused(timer, !timer.IsPaused);



    [RelayCommand] private void OnFinishPressed() => timers.Finish(timer);



    [RelayCommand] private void OnCancelPressed() => timers.Kill(timer);



    [RelayCommand] private void OnAddTimePressed()
    {
        if (addAmount() is { } amount) timers.AddTime(timer, amount);
    }



    public override void Dispose() { }
}
