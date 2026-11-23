using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace StreamAssist.Scripting;



/// <summary>
/// A binding written in C#: the app makes a new one each time its trigger fires and calls <see cref="RunAsync"/>
/// (a key or microphone also calls <see cref="OnReleasedAsync"/> when it's let go). Everything the app can do is on
/// its properties: <see cref="Twitch"/>, <see cref="Obs"/>, <see cref="Spotify"/>, <see cref="Voicemeeter"/>,
/// <see cref="Sound"/>, <see cref="Data"/>, <see cref="Dashboard"/>, <see cref="Widgets"/>, <see cref="Input"/> and
/// <see cref="App"/>. A failure (a <see cref="StreamAssistException"/> or any other exception) is logged in the
/// console with this binding's name and ends the run.
/// <para>
/// Each run is on a background thread, so a busy or stuck script never freezes the app; what it asks the app to do is
/// done on the app's UI thread for it. The bottom panel's limits stop a run whose loop goes on longer than the
/// Loop limit without waiting for anything (await <see cref="WaitAsync(double)"/> in a loop that should keep
/// going), and a run that takes longer than its Run limit. Runs can happen at the same time (two chat commands at
/// once): to read and then change a table or variable without another run changing it in between, lock it
/// (<see cref="IData.LockTableAsync"/>); for data scripts share themselves (a static field), use
/// <see cref="IApp.LockAsync"/>.
/// </para>
/// </summary>
public abstract class StreamAssistScript
{
    private IScriptRun? _run;



    /// <summary>The run this script is part of (set by the app before it calls the script).</summary>
    private IScriptRun Current => _run ?? throw new InvalidOperationException("this script isn't running (only the app can run it)");



    /// <summary>Called by the app before running the script; scripts never call this.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void AttachRun(IScriptRun run) => _run = run;



    // what happens



    /// <summary>
    /// Runs when the trigger fires: a chat command, redeem, alert, dock button, key or microphone press, Run Script,
    /// or the Test button.
    /// </summary>
    public abstract Task RunAsync();



    /// <summary>Runs when a held key or button is let go, or a microphone stops talking. Does nothing unless overridden.</summary>
    public virtual Task OnReleasedAsync() => Task.CompletedTask;



    // the run



    /// <summary>
    /// This run's parameters, from what triggered it (e.g. a chat command's <c>user</c>, <c>message</c>, or what its
    /// Parse Parameter blocks read). The bottom panel's Parameters list shows which ones a binding gets.
    /// </summary>
    protected IParameters Parameters => Current.Parameters;

    /// <summary>Cancelled when the run should stop (the binding was removed, the app is closing, its Run limit); waits stop by themselves.</summary>
    protected CancellationToken CancellationToken => Current.CancellationToken;

    /// <summary>Waits this many seconds (decimals work, e.g. 0.5), stopping early if the run is cancelled.</summary>
    protected Task WaitAsync(double seconds) => Current.WaitAsync(TimeSpan.FromSeconds(Math.Max(0, seconds)));

    /// <summary>Waits this long, stopping early if the run is cancelled.</summary>
    protected Task WaitAsync(TimeSpan time) => Current.WaitAsync(time);

    /// <summary>Ends this run quietly, like the Stop block (nothing is logged). Returning from RunAsync does the same.</summary>
    protected void Stop() => throw new ScriptStoppedException();



    // the app



    /// <summary>Twitch: chat, moderation, stream info, polls, predictions, clips, channel point rewards.</summary>
    protected ITwitch Twitch => Current.Api.Twitch;

    /// <summary>OBS: scenes, sources, filters, audio, outputs.</summary>
    protected IObs Obs => Current.Api.Obs;

    /// <summary>Spotify: playing, queueing, playback settings.</summary>
    protected ISpotify Spotify => Current.Api.Spotify;

    /// <summary>Voicemeeter: mutes, faders, routing, any setting.</summary>
    protected IVoicemeeter Voicemeeter => Current.Api.Voicemeeter;

    /// <summary>Sounds and text to speech.</summary>
    protected ISound Sound => Current.Api.Sound;

    /// <summary>Variables, tables and cooldowns (the Data tab, kept after the app closes).</summary>
    protected IData Data => Current.Api.Data;

    /// <summary>The Dashboard's docks: console docks, indicators, timers, sound docks.</summary>
    protected IDashboard Dashboard => Current.Api.Dashboard;

    /// <summary>Widgets on stream: wheels, plinko, image party, mic bounce, clips.</summary>
    protected IWidgets Widgets => Current.Api.Widgets;

    /// <summary>Keyboards, mice, controllers and microphones: what's held right now.</summary>
    protected IInput Input => Current.Api.Input;

    /// <summary>The app itself: the console, configs, other scripts.</summary>
    protected IApp App => Current.Api.App;
}



/// <summary>What the app gives a script for one run; scripts use <see cref="StreamAssistScript"/>'s members instead.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IScriptRun
{
    IParameters Parameters { get; }
    CancellationToken CancellationToken { get; }
    IScriptApi Api { get; }
    Task WaitAsync(TimeSpan time);
}



/// <summary>Everything the app can do, one property per part of it (blocks use this too).</summary>
public interface IScriptApi
{
    ITwitch Twitch { get; }
    IObs Obs { get; }
    ISpotify Spotify { get; }
    IVoicemeeter Voicemeeter { get; }
    ISound Sound { get; }
    IData Data { get; }
    IDashboard Dashboard { get; }
    IWidgets Widgets { get; }
    IInput Input { get; }
    IApp App { get; }
}



/// <summary>A run's parameters: named text values, e.g. <c>Parameters["user"]</c>.</summary>
public interface IParameters : IEnumerable<KeyValuePair<string, string>>
{
    /// <summary>A parameter's value; a missing one is an error (use <see cref="Get"/> for one that may not be there).</summary>
    string this[string name] { get; set; }

    /// <summary>A parameter's value, or <paramref name="fallback"/> if this run doesn't have it.</summary>
    string Get(string name, string fallback = "");

    /// <summary>Whether this run has the parameter.</summary>
    bool Contains(string name);
}
