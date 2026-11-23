using System.Collections.Generic;
using System.Threading.Tasks;

namespace StreamAssist.Scripting;



/// <summary>The app itself: its console, configs and the Scripts tab.</summary>
public interface IApp
{
    /// <summary>Prints a line in the app console (on the left), like the App Console Message block.</summary>
    void Log(string message);

    /// <summary>
    /// Switches a tab to its config with this name (case ignored), as if it was picked in the tab's config bar.
    /// Already in use: nothing happens.
    /// </summary>
    void SetConfig(ConfigTab tab, string name);

    /// <summary>The name of the config a tab is using, null if it has none.</summary>
    string? GetConfig(ConfigTab tab);

    /// <summary>
    /// Runs a script from the Scripts tab (the config in use) by name, with this run's parameters plus
    /// <paramref name="parameters"/>, and waits until it's done. False if it didn't run (it's off or empty).
    /// </summary>
    Task<bool> RunScriptAsync(string name, IReadOnlyDictionary<string, string>? parameters = null);

    /// <summary>Starts a script from the Scripts tab and carries on straight away. False if it didn't start.</summary>
    bool StartScript(string name, IReadOnlyDictionary<string, string>? parameters = null);

    /// <summary>
    /// Takes a lock by name (any name you like, case ignored) until it's disposed; another run asking for the same
    /// name waits until it's let go (at most the binding's Lock wait, then fails). Only runs that ask for it wait, so
    /// it's for your own things scripts share, e.g. a static field in a Shared helper two scripts change.
    /// <code>using (await App.LockAsync("score")) { ... }</code>
    /// </summary>
    Task<HeldLock> LockAsync(string name);
}



/// <summary>A tab with configs.</summary>
public enum ConfigTab { Input, Chat, Events, Dashboard, Scripts, Themes }
