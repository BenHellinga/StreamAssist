using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Dashboard.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Dashboard.UI;



// a sound dock's body: the playing sounds at the top, then the ones waiting their turn, each with a stop button. The
// header's buttons skip the playing ones or stop them all, its checkbox picks one at a time or over each other
public sealed partial class SoundDockViewModel : ViewModelBase
{
    // private



    private readonly SoundsModel _sounds;

    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _anyRunning; // any playing and not paused: the header's pause pauses them, else it resumes all
    [ObservableProperty] private bool _waitUntilDone; // the header's checkbox: one at a time, or over each other



    // public



    public ObservableCollection<SoundRowViewModel> Rows { get; }



    // constructor



    public SoundDockViewModel(SoundsModel sounds)
    {
        _sounds = sounds;
        Rows = [];
        _waitUntilDone = _sounds.WaitUntilDone;

        Refresh();

        _sounds.SoundsChanged += Refresh;
        _sounds.WaitUntilDoneChanged += OnSoundsWaitUntilDoneChanged;
    }



    // header buttons



    partial void OnWaitUntilDoneChanged(bool value) => _sounds.SetWaitUntilDone(value);



    [RelayCommand] private void OnPauseAllPressed() => _sounds.SetAllPaused(AnyRunning);



    [RelayCommand] private void OnSkipPressed() => _sounds.StopPlaying();



    [RelayCommand] private void OnStopAllPressed() => _sounds.StopAll();



    // model events



    private void OnSoundsWaitUntilDoneChanged() => WaitUntilDone = _sounds.WaitUntilDone;



    // helpers



    private void Refresh()
    {
        if (!_sounds.Sounds.SequenceEqual(Rows.Select(r => r.Sound)))
        {
            Rows.Clear();
            foreach (var sound in _sounds.Sounds) Rows.Add(new SoundRowViewModel(_sounds, sound));
        }

        foreach (var row in Rows) row.Update();
        IsEmpty = Rows.Count == 0;
        AnyRunning = _sounds.Sounds.Any(s => s.IsPlaying && !s.IsPaused);
    }



    // disposal



    public override void Dispose()
    {
        _sounds.SoundsChanged -= Refresh;
        _sounds.WaitUntilDoneChanged -= OnSoundsWaitUntilDoneChanged;
    }
}



// one sound: what it is, playing / paused / waiting, pause (play while paused) and stop
public sealed partial class SoundRowViewModel(SoundsModel sounds, QueuedSound sound) : ViewModelBase
{
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(State))] private bool _isPlaying;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(State))] private bool _isPaused;

    public string State => IsPaused ? "Paused" : IsPlaying ? "Playing" : "Waiting";



    public QueuedSound Sound => sound;
    public string Label => sound.Label;



    public void Update()
    {
        IsPlaying = sound.IsPlaying;
        IsPaused = sound.IsPaused;
    }



    [RelayCommand] private void OnPausePressed() => sounds.SetPaused(sound, !sound.IsPaused);



    [RelayCommand] private void OnStopPressed() => sounds.Stop(sound);



    public override void Dispose() { }
}
