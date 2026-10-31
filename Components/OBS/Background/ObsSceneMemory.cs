using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StreamAssist.Components.Data.Model;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.OBS.Background;



// the last few scenes left, for Return to Previous Scene, like undo: every switch made by a block puts the scene it
// leaves first (whatever was live, even if picked in OBS itself), and returning takes the first one off without adding
// anything, so pressing it again keeps going back (Minecraft -> BRB -> Ending, then back twice: Ending -> BRB -> Minecraft).
// Kept as Text variables previousScene1 (the most recent) ... previousScene5, made when first needed, so it survives a restart
public static class ObsSceneMemory
{
    // constants



    public const int Size = 5;
    public const string RowPrefix = "previousScene"; // previousScene1 ... previousScene5



    // public api



    // switches and remembers the scene left; false if OBS refused or isn't connected (already logged)
    public static async Task<bool> TrySwitchAsync(ObsManager obs, DataModel data, string scene)
    {
        // only asked while connected, so a switch without OBS fails with the switch's own error
        string? leaving = null;
        if (obs.IsConnected && await obs.TryGetCurrentSceneAsync() is (true, var current)) leaving = current;

        if (!(await obs.TrySetCurrentSceneAsync(scene)).Ok) return false;

        if (leaving != null && !Same(leaving, scene)) Save([leaving, .. Load(data)], data);
        return true;
    }



    // back to the most recent scene left (skipping one that's already live), taking it off the list; false (logged)
    // if none is remembered or OBS refused
    public static async Task<bool> TryReturnAsync(ObsManager obs, DataModel data)
    {
        var history = Load(data);
        string? live = null;
        if (obs.IsConnected && await obs.TryGetCurrentSceneAsync() is (true, var current)) live = current;
        while (history.Count > 0 && live != null && Same(history[0], live)) history.RemoveAt(0);

        if (history.Count == 0)
        {
            Save(history, data);
            AppLog.WriteError("can't go back a scene, none remembered");
            return false;
        }

        if (!(await obs.TrySetCurrentSceneAsync(history[0])).Ok) return false;

        history.RemoveAt(0);
        Save(history, data);
        return true;
    }



    // the remembered scenes, most recent first
    public static List<string> Load(DataModel data) =>
        Enumerable.Range(1, Size).Select(i => data.GetVariableText(RowPrefix + i)).Where(s => s != "").ToList();



    // helpers



    // the newest Size of them, an emptied place saved as ""
    private static void Save(List<string> history, DataModel data)
    {
        for (int i = 0; i < Size; i++)
        {
            var value = i < history.Count ? history[i] : "";
            if (data.GetVariableText(RowPrefix + (i + 1)) != value) data.SetVariableText(RowPrefix + (i + 1), value);
        }
    }



    private static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.Ordinal);
}
