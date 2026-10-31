using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace StreamAssist.Core.Persistence;



// loads and saves one JSON file per feature in Config/ (keyboard.json, chatbot.json, ...).
// A file that can't be read is never overwritten: Load gives defaults and marks it broken, Save then skips it, and its
// tab says so (Back up & start fresh renames it so saving can carry on). LoadOrReset is for small settings files that
// aren't worth a locked tab: a broken one is backed up and replaced with defaults. Saves write a temporary file and swap
// it in, so a crash mid-save can't leave a half-written file
public static class JsonFileStore
{
    // private



    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly Dictionary<string, string> Broken = []; // file name → why it can't be read
    private static readonly object BrokenLock = new();



    // public



    // where the files are (the tests point it at a temporary folder)
    public static string RootDir { get; set; } = AppPaths.ConfigDir; // Config/ in the app's folder (see AppPaths)
    // public static string RootDir { get; set; } = Path.Combine(
    //     Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    //     "StreamAssist");



    // how problems are reported (Core doesn't know the console: the app connects these to it at startup)
    public static Action<string> ReportError { get; set; } = _ => { };
    public static Action<string> ReportWarning { get; set; } = _ => { };



    // events



    public static event Action<string>? BrokenChanged; // a file became broken, or was backed up (its name)



    // public api



    // the file's data, or defaults if it doesn't exist yet. If it can't be read: defaults, the file is marked broken
    // (never saved over) and that's logged once
    public static T Load<T>(string name) where T : class, new()
    {
        var (data, error) = Read<T>(name);
        if (error == null) return data;

        lock (BrokenLock)
        {
            if (Broken.ContainsKey(name)) return data;
            Broken[name] = error;
        }

        ReportError($"{name}.json can't be read, not saving it ({error})");
        BrokenChanged?.Invoke(name);
        return data;
    }



    // for small settings files: one that can't be read is backed up (name.broken-<time>.json) and replaced with defaults
    public static T LoadOrReset<T>(string name) where T : class, new()
    {
        var (data, error) = Read<T>(name);
        if (error == null) return data;

        var backup = TryBackUp(name, out var backupError);
        if (backup != null) ReportWarning($"{name}.json can't be read, backed up as {backup} ({error})");
        else ReportError($"{name}.json can't be read or backed up ({backupError})");
        return data;
    }



    // writes the file (skipped while it's broken), through a temporary file swapped in, retried a few times in case
    // something (e.g. OneDrive syncing) has it open for a moment
    public static void Save<T>(string name, T data)
    {
        if (IsBroken(name)) return;

        var path = PathOf(name);
        var temp = path + ".tmp";

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                Directory.CreateDirectory(RootDir);
                File.WriteAllText(temp, JsonSerializer.Serialize(data, JsonOptions));
                File.Move(temp, path, overwrite: true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt < 3)
                {
                    Thread.Sleep(100);
                    continue;
                }

                ReportError($"can't save {name}.json ({ex.Message})");
                return;
            }
        }
    }



    public static bool IsBroken(string name)
    {
        lock (BrokenLock) return Broken.ContainsKey(name);
    }



    // why a broken file can't be read (e.g. "line 12, position 5"), null if it isn't broken
    public static string? BrokenReason(string name)
    {
        lock (BrokenLock) return Broken.TryGetValue(name, out var reason) ? reason : null;
    }



    // renames a broken file out of the way (name.broken-<time>.json) so saving carries on from what's in memory now
    // (the defaults it started with, plus anything changed since). Doing it for a file that isn't broken does nothing
    public static bool TryBackUpAndStartFresh(string name)
    {
        if (!IsBroken(name)) return true;

        var backup = TryBackUp(name, out var error);
        if (backup == null)
        {
            ReportError($"can't back up {name}.json ({error})");
            return false;
        }

        lock (BrokenLock) Broken.Remove(name);
        BrokenChanged?.Invoke(name);
        return true;
    }



    // helpers



    // the data (defaults when missing or unreadable) and why it couldn't be read, null if it could
    private static (T Data, string? Error) Read<T>(string name) where T : class, new()
    {
        var path = PathOf(name);
        if (!File.Exists(path)) return (new T(), null);

        try
        {
            return (JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions) ?? new T(), null);
        }
        catch (JsonException ex)
        {
            var where = ex.LineNumber is { } line ? $"line {line + 1}, position {(ex.BytePositionInLine ?? 0) + 1}" : ex.Message;
            return (new T(), where);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return (new T(), ex.Message);
        }
    }



    // the backup's file name, null if it couldn't be renamed
    private static string? TryBackUp(string name, out string? error)
    {
        var backup = $"{name}.broken-{DateTime.Now:yyyy-MM-dd-HHmmss}.json";
        try
        {
            File.Move(PathOf(name), Path.Combine(RootDir, backup));
            error = null;
            return backup;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return null;
        }
    }



    private static string PathOf(string name) => Path.Combine(RootDir, $"{name}.json");
}
