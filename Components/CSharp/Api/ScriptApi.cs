using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.CSharp.Background;
using StreamAssist.Core.Utilities;
using StreamAssist.Scripting;
using StreamAssist.Scripting.Runtime;

namespace StreamAssist.Components.CSharp.Api;



// the app's API (StreamAssist.Scripting) for one run: what a C# script calls, and what blocks call to do their work, so
// each thing the app can do is written once. Made per run from its TriggerContext (parameters, cancellation, the chat
// message that started it), so calls need neither. A failure throws StreamAssistException: IsLogged when a manager
// already logged it (e.g. a twitch request), otherwise whoever catches it logs it under their name (a block's, a script's)
public sealed class ScriptApi(TriggerContext context) : IScriptApi
{
    private TwitchScriptApi? _twitch;
    private ObsScriptApi? _obs;
    private SpotifyScriptApi? _spotify;
    private VoicemeeterScriptApi? _voicemeeter;
    private SoundScriptApi? _sound;
    private DataScriptApi? _data;
    private DashboardScriptApi? _dashboard;
    private WidgetsScriptApi? _widgets;
    private InputScriptApi? _input;
    private AppScriptApi? _app;



    public TriggerContext Context => context;

    public ITwitch Twitch => _twitch ??= new(context);
    public IObs Obs => _obs ??= new(context);
    public ISpotify Spotify => _spotify ??= new(context);
    public IVoicemeeter Voicemeeter => _voicemeeter ??= new(context);
    public ISound Sound => _sound ??= OperatingSystem.IsWindows() ? new(context) : throw new PlatformNotSupportedException("sound needs windows");
    public IData Data => _data ??= new(context);
    public IDashboard Dashboard => _dashboard ??= new(context);
    public IWidgets Widgets => _widgets ??= new(context);
    public IInput Input => _input ??= new(context);
    public IApp App => _app ??= new(context);



    // the typed versions, for blocks (they reach a few things scripts don't need, e.g. a run's TriggerContext)
    public TwitchScriptApi TwitchApi => (TwitchScriptApi)Twitch;
    public ObsScriptApi ObsApi => (ObsScriptApi)Obs;
    public SpotifyScriptApi SpotifyApi => (SpotifyScriptApi)Spotify;
    public VoicemeeterScriptApi VoicemeeterApi => (VoicemeeterScriptApi)Voicemeeter;
    public SoundScriptApi SoundApi => (SoundScriptApi)Sound;
    public DataScriptApi DataApi => (DataScriptApi)Data;
    public DashboardScriptApi DashboardApi => (DashboardScriptApi)Dashboard;
    public WidgetsScriptApi WidgetsApi => (WidgetsScriptApi)Widgets;
    public InputScriptApi InputApi => (InputScriptApi)Input;
    public AppScriptApi AppApi => (AppScriptApi)App;
}



// one part of the API (Twitch, OBS, ...), for one run
public abstract class ScriptApiPart(TriggerContext context)
{
    protected TriggerContext Context => context;
    protected CancellationToken Token => context.CancellationToken;



    // something wrong with what was asked (e.g. a number out of range): not logged yet, the caller logs it
    protected static StreamAssistException Error(string reason) => new(reason);

    // a manager already logged why (e.g. a request that failed)
    protected static StreamAssistException Logged(string reason) => new(reason, true);

    // a manager or model the app makes at startup (missing in the tests, or before startup finished)
    protected static T Need<T>(T? service, string what) where T : class => service ?? throw Error($"{what} not set up");



    // a script's callback (a timer's whenDone, a wheel's), made on the run's thread: when it's called (by a manager, on
    // the UI thread) it runs on a background thread as part of this run, with a guard of its own under the run's (the
    // same limits, its locks let go when it ends), stopped with the binding but not by the run limit (it outlives the
    // run). Its failure is logged with what it was for, e.g. "timer 'raid' when done: ..."
    protected Action Later(string what, Func<Task> callback)
    {
        var parent = ScriptGuard.Current;
        var token = context.LastingCancellationToken;
        return UiThread.Later(() => _ = RunLaterAsync(what, callback, parent, token))!;
    }

    protected Action<T1> Later<T1>(string what, Func<T1, Task> callback)
    {
        var parent = ScriptGuard.Current;
        var token = context.LastingCancellationToken;
        return UiThread.Later<T1>(a => _ = RunLaterAsync(what, () => callback(a), parent, token))!;
    }

    protected Action<T1, T2> Later<T1, T2>(string what, Func<T1, T2, Task> callback)
    {
        var parent = ScriptGuard.Current;
        var token = context.LastingCancellationToken;
        return UiThread.Later<T1, T2>((a, b) => _ = RunLaterAsync(what, () => callback(a, b), parent, token))!;
    }



    private static async Task RunLaterAsync(string what, Func<Task> callback, ScriptGuard? parent, CancellationToken token)
    {
        var guard = parent == null ? null : new ScriptGuard(parent, parent.Title, token, parent.LoopLimit, parent.LockWait);
        guard?.Use();
        try
        {
            await callback();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // its binding was removed
        }
        catch (Exception ex)
        {
            ScriptManager.LogFailure(what, ex, "");
        }
        finally
        {
            if (guard != null) ScriptLocks.ReleaseAll(guard);
        }
    }
}



// the run a script is given: its parameters, cancellation, API and waits
public sealed class ScriptRun(TriggerContext context) : IScriptRun
{
    private readonly ScriptParameters _parameters = new(context);
    private readonly ScriptApi _api = new(context);

    public IParameters Parameters => _parameters;
    public CancellationToken CancellationToken => context.CancellationToken;
    public IScriptApi Api => _api;

    public Task WaitAsync(TimeSpan time) => Task.Delay(time < TimeSpan.Zero ? TimeSpan.Zero : time, context.CancellationToken);
}



// a run's parameters for a script: reading a missing one is an error (Get gives a fallback), setting one is for the rest
// of the run (as Set Parameter), and blocks the script runs see it
public sealed class ScriptParameters(TriggerContext context) : IParameters
{
    public string this[string name]
    {
        get => context.Parameters.TryGetValue(name, out var value) ? value : throw new StreamAssistException($"no parameter '{name}'");
        set
        {
            if (!TriggerContext.IsValidName(name)) throw new StreamAssistException($"'{name}' isn't a valid name, use letters, digits, _");
            context.SetParameter(name, value ?? "");
        }
    }

    public string Get(string name, string fallback = "") => context.Parameters.TryGetValue(name, out var value) ? value : fallback;

    public bool Contains(string name) => context.Parameters.ContainsKey(name);

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => context.Parameters.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
