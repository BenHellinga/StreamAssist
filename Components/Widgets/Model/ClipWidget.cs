using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Widgets.Model;



// a Twitch clip to play: its id (for Twitch's embedded player), link, title, channel's display name, length in seconds
// and a still of it (its thumbnail's address, "" for none: the box's preview shows it while it plays)
public sealed record ClipInfo(string Id, string Url, string Title, string Channel, double Seconds, string Thumbnail = "");



// the clip playing, and when it started
public sealed record ClipPlay(ClipInfo Clip, DateTime StartedAt);



// plays Twitch clips on stream (the Play Clip block, or its box's Play Clip, picks a random one from a channel, e.g. a
// shoutout's): its page shows Twitch's own clip player, with sound, for the clip's length (plus a moment for the player
// to load). Clips asked for while one plays wait in a queue and play in turn; Next skips to the next one, Stop ends it
// and empties the queue. Its settings are saved; what's playing and queued only live while the app runs. The
// WidgetManager times each clip (FinishPlay) and fetches the clips its box asks for (ClipRequested), its pages show them
public sealed class ClipWidget : WidgetModel
{
    // constants



    public const double LoadSeconds = 1.5; // Twitch's player takes a moment to start: the clip shows this much longer
    public const double MaxSeconds = 600;
    public const int MaxTopClips = 100; // Twitch gives at most 100 clips in one go



    // private



    private double? _maxClipSeconds; // clips longer than this aren't picked, null: any length
    private int _topClips; // a clip is picked from this many of the most viewed (of those short enough)
    private ClipPlay? _playing;
    private Action? _onDone;
    private readonly Queue<(ClipInfo Clip, Action? OnDone)> _queue;
    private ClipInfo? _lastClip;



    // public



    [JsonIgnore] public override WidgetKind Kind => WidgetKind.Clip;
    [JsonIgnore] public double? MaxClipSeconds => _maxClipSeconds;
    [JsonIgnore] public int TopClips => _topClips;
    [JsonIgnore] public ClipPlay? Playing => _playing; // null while nothing plays
    [JsonIgnore] public bool IsPlaying => _playing != null;
    [JsonIgnore] public int QueueCount => _queue.Count; // waiting after the one playing
    [JsonIgnore] public IReadOnlyList<ClipInfo> Queued => _queue.Select(q => q.Clip).ToList();
    [JsonIgnore] public ClipInfo? LastClip => _lastClip; // the last one played this session

    // what it's doing, in a few words: "Idle", "Now playing: Hoss", "Now playing: Hoss, and 8 more"
    [JsonIgnore] public string Status => _playing is not { } play ? "Idle"
        : _queue.Count == 0 ? $"Now playing: {play.Clip.Channel}" : $"Now playing: {play.Clip.Channel}, and {_queue.Count} more";



    // json



    [JsonInclude] [JsonPropertyName("maxClipSeconds")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private double? JsonMaxClipSeconds
    {
        get => _maxClipSeconds;
        set => _maxClipSeconds = value is > 0 and <= MaxSeconds ? value : null;
    }

    [JsonInclude] [JsonPropertyName("topClips")]
    private int JsonTopClips
    {
        get => _topClips;
        set => _topClips = value is >= 1 and <= MaxTopClips ? value : MaxTopClips;
    }



    // events



    public event Action? PlayStarted;
    public event Action? PlaybackChanged; // a clip started or ended, or the queue changed
    public event Action<ClipWidget, string>? ClipRequested; // its box asked for a clip of this channel (the WidgetManager fetches one)



    // constructor



    public ClipWidget()
    {
        _maxClipSeconds = null;
        _topClips = MaxTopClips;
        _playing = null;
        _onDone = null;
        _queue = new();
        _lastClip = null;
    }



    // public api: settings



    // null: any length
    public bool TrySetMaxClipSeconds(double? seconds)
    {
        if (seconds is not null and not (> 0 and <= MaxSeconds))
        {
            AppLog.WriteError($"can't set clip max length to {seconds}, it's above 0 and up to {MaxSeconds} seconds (empty: any length)");
            return false;
        }

        if (seconds != _maxClipSeconds)
        {
            _maxClipSeconds = seconds;
            RaiseSettingsChanged();
        }
        return true;
    }



    public bool TrySetTopClips(int top)
    {
        if (top is < 1 or > MaxTopClips)
        {
            AppLog.WriteError($"can't set clip top to {top}, it's 1 to {MaxTopClips}");
            return false;
        }

        if (top != _topClips)
        {
            _topClips = top;
            RaiseSettingsChanged();
        }
        return true;
    }



    // whether a clip is short enough to be picked
    public bool Allows(ClipInfo clip) => _maxClipSeconds is not { } max || clip.Seconds <= max;



    // public api: playing



    // its box's Play Clip: a random clip of that channel, fetched by the WidgetManager, then played or queued
    public bool TryRequestClip(string channel)
    {
        var name = channel.Trim().TrimStart('@');
        if (name == "")
        {
            AppLog.WriteError($"can't play a clip on '{Name}', no channel");
            return false;
        }

        if (IsDisabled) return false;
        ClipRequested?.Invoke(this, name);
        return true;
    }



    // plays the clip now, or queues it after the ones waiting while one plays. False (quietly nothing) while it's off.
    // onDone runs once it has ended (or been skipped)
    public bool TryPlay(ClipInfo clip, Action? onDone, DateTime? now = null)
    {
        if (IsDisabled) return false;

        _queue.Enqueue((clip, onDone));
        if (!IsPlaying) StartNext(now);
        else PlaybackChanged?.Invoke();
        return true;
    }



    // the box's Next: this clip ends now (its When Done runs), the next one plays
    public void Next() => FinishPlay(_playing)?.Invoke();



    // the box's Stop: this clip ends now (its When Done runs), the waiting ones are dropped (theirs never run)
    public void Stop()
    {
        _queue.Clear();
        FinishPlay(_playing)?.Invoke();
        PlaybackChanged?.Invoke();
    }



    // that clip has ended (the WidgetManager, once its time is up; Next and Stop, early): the next one waiting plays.
    // Only that play: an old clip's timing doesn't end a newer one. Gives back its callback (Play Clip's When Done)
    // for the caller to run
    public Action? FinishPlay(ClipPlay? play)
    {
        if (_playing == null || _playing != play) return null;

        var onDone = _onDone;
        _playing = null;
        _onDone = null;
        StartNext(null);
        if (_playing == null)
        {
            PlaybackChanged?.Invoke();
            RaiseDrawChanged();
        }
        return onDone;
    }



    // helpers



    private void StartNext(DateTime? now)
    {
        if (_queue.Count == 0) return;

        var (clip, onDone) = _queue.Dequeue();
        _playing = new(clip, now ?? DateTime.UtcNow);
        _lastClip = clip;
        _onDone = onDone;
        PlayStarted?.Invoke();
        PlaybackChanged?.Invoke();
        RaiseDrawChanged();
    }



    // disposal



    public override void Dispose()
    {
        _queue.Clear();
        _playing = null;
        _onDone = null;
    }
}
