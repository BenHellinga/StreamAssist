using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StreamAssist.Components.Dashboard.Model;



// a sound ready to play on a device (made by the Audio component: a file, or Speak Text's speech). Start plays it and
// calls finished on the UI thread when it has played or was stopped; Pause / Resume hold it where it is; Stop ends it
// (or, never started, frees it)
public interface ISoundPlayback
{
    void Start(Action finished);
    void Pause();
    void Resume();
    void Stop();
}



// one sound sent to a sound dock: waiting its turn, then playing. Ready is the playback, or null if it couldn't be
// made (Speak Text's speech is still being made while it waits)
public sealed class QueuedSound
{
    public string Label { get; }
    public bool IsPlaying { get; internal set; } // started (paused or not)
    public bool IsPaused { get; internal set; }
    public Task Done => _done.Task; // played, stopped, or couldn't play: a Speak Text that waits until done goes on

    internal Task<ISoundPlayback?> Ready { get; }
    internal ISoundPlayback? Playback { get; set; }
    internal bool IsStarting { get; set; } // its turn came, it's waiting to be made

    private readonly TaskCompletionSource _done = new();



    internal QueuedSound(string label, Task<ISoundPlayback?> ready)
    {
        Label = label;
        Ready = ready;
    }



    internal void SetDone() => _done.TrySetResult();
}



// one sound dock's sounds (DockModel.Sounds), only kept while the app runs, each of them pausable and stoppable. With
// Wait Until Done (saved with the dock) they play one at a time in the order they were sent (a sound still being made,
// e.g. speech, keeps its place; a paused one holds the rest); without it each plays as soon as it's ready, over the
// others. A sound played without a
// dock isn't here: it plays straight away and nothing can stop it. While the dock is turned off nothing changes them
// (quietly): a sound sent to it doesn't play, stop does nothing, the ones there carry on
public sealed class SoundsModel : IDisposable
{
    // private



    private readonly List<QueuedSound> _sounds;
    private bool _waitUntilDone;
    private bool _isOff;
    private bool _isDisposed;



    // public



    public IReadOnlyList<QueuedSound> Sounds => _sounds; // the playing ones first, then the waiting ones in order
    public bool WaitUntilDone => _waitUntilDone;
    public bool IsOff => _isOff;



    // events



    public event Action? SoundsChanged; // one was added, started, paused, resumed, ended or was stopped
    public event Action? WaitUntilDoneChanged;



    // constructor



    public SoundsModel()
    {
        _sounds = [];
        _waitUntilDone = true;
        _isOff = false;
        _isDisposed = false;
    }



    // public api



    // sends a sound here: it plays when its turn comes (null while the dock is off: it never plays, its playback is
    // freed)
    public QueuedSound? Enqueue(string label, Task<ISoundPlayback?> ready)
    {
        if (_isOff || _isDisposed)
        {
            _ = FreeWhenReadyAsync(ready);
            return null;
        }

        var sound = new QueuedSound(label, ready);
        _sounds.Add(sound);
        SoundsChanged?.Invoke();

        StartWhatCan();
        return sound;
    }



    // turned on, sounds sent from now wait for the ones playing; turned off, the waiting ones start now
    public void SetWaitUntilDone(bool waitUntilDone)
    {
        if (waitUntilDone == _waitUntilDone) return;

        _waitUntilDone = waitUntilDone;
        WaitUntilDoneChanged?.Invoke();
        StartWhatCan();
    }



    // pauses or resumes a playing one (a waiting one has nothing to pause)
    public void SetPaused(QueuedSound sound, bool isPaused)
    {
        if (_isOff || !_sounds.Contains(sound) || sound.Playback is not { } playback || sound.IsPaused == isPaused) return;

        sound.IsPaused = isPaused;
        if (isPaused) playback.Pause();
        else playback.Resume();
        SoundsChanged?.Invoke();
    }



    // every playing one (the header's pause): paused, or all resumed
    public void SetAllPaused(bool isPaused)
    {
        if (_isOff) return;
        foreach (var sound in _sounds.Where(s => s.IsPlaying).ToList()) SetPaused(sound, isPaused);
    }



    // stops one: playing, the next can start; waiting, it's taken out
    public void Stop(QueuedSound sound)
    {
        if (_isOff || !_sounds.Contains(sound)) return;
        Remove(sound);
    }



    // the playing ones (the next can start)
    public void StopPlaying()
    {
        if (_isOff) return;
        foreach (var sound in _sounds.Where(s => s.IsPlaying).ToList()) Remove(sound);
    }



    // every one, playing and waiting
    public void StopAll()
    {
        if (_isOff) return;
        StopEverything();
    }



    internal void SetIsOff(bool isOff) => _isOff = isOff;



    // helpers



    // waiting until done: the first one waiting, once none is playing or being made; otherwise every one waiting
    private void StartWhatCan()
    {
        var waiting = _sounds.Where(s => !s.IsPlaying && !s.IsStarting).ToList();
        if (_waitUntilDone)
        {
            if (waiting.Count > 0 && !_sounds.Any(s => s.IsPlaying || s.IsStarting)) _ = StartAsync(waiting[0]);
            return;
        }

        foreach (var sound in waiting) _ = StartAsync(sound);
    }



    // plays it once it's made; if it was stopped while it was being made, whoever stopped it already moved on
    private async Task StartAsync(QueuedSound sound)
    {
        sound.IsStarting = true;
        var playback = await sound.Ready;
        sound.IsStarting = false;
        if (!_sounds.Contains(sound)) return;

        if (playback == null)
        {
            Remove(sound); // couldn't be made (already logged): the next one
            return;
        }

        sound.Playback = playback;
        sound.IsPlaying = true;
        SoundsChanged?.Invoke();
        playback.Start(() => OnFinished(sound));
    }



    private void Remove(QueuedSound sound)
    {
        _sounds.Remove(sound);
        End(sound);
        SoundsChanged?.Invoke();
        StartWhatCan();
    }



    private void OnFinished(QueuedSound sound)
    {
        if (_sounds.Contains(sound)) Remove(sound);
    }



    // stopped or played: its playback is stopped and freed (one still being made is freed once it's made)
    private static void End(QueuedSound sound)
    {
        if (sound.Playback != null) sound.Playback.Stop();
        else _ = FreeWhenReadyAsync(sound.Ready);
        sound.SetDone();
    }



    private static async Task FreeWhenReadyAsync(Task<ISoundPlayback?> ready)
    {
        try
        {
            (await ready)?.Stop();
        }
        catch (Exception)
        {
            // it failed to be made, there's nothing to free
        }
    }



    private void StopEverything()
    {
        var all = _sounds.ToList();
        _sounds.Clear();
        foreach (var sound in all) End(sound);
        if (all.Count > 0) SoundsChanged?.Invoke();
    }



    // disposal: a removed dock's sounds stop



    public void Dispose()
    {
        _isDisposed = true;
        StopEverything();
    }
}
