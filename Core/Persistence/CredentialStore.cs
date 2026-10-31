using System.Collections.Generic;
using System.Text.Json;

namespace StreamAssist.Core.Persistence;



// every secret the app keeps (app credentials, login tokens, ...) in one file, Config/auth.json (git ignored)
// each service reads and writes its own section, e.g. "twitchApps", "twitchLogins", later "spotify"
public static class CredentialStore
{
    // private



    public const string FileName = "auth";
    private static readonly object FileLock = new(); // sections can be saved from different threads



    // public api



    // false while auth.json can't be read: nothing is saved to it then, and connections refuse to start (they'd log in
    // without being able to keep the login)
    public static bool IsReadable
    {
        get
        {
            lock (FileLock)
            {
                JsonFileStore.Load<Dictionary<string, JsonElement>>(FileName); // finds out (and reports it once) if it isn't
                return !JsonFileStore.IsBroken(FileName);
            }
        }
    }



    // a section's data, or a fresh default if it isn't there yet
    public static T Load<T>(string section) where T : class, new()
    {
        lock (FileLock)
        {
            var file = JsonFileStore.Load<Dictionary<string, JsonElement>>(FileName);

            try
            {
                return file.TryGetValue(section, out var data) ? data.Deserialize<T>() ?? new T() : new T();
            }
            catch (JsonException)
            {
                return new T(); // a broken section counts as empty, the other sections are untouched
            }
        }
    }



    // replaces one section, leaving the others as they are (nothing is written while the file can't be read, so the other
    // sections are never lost)
    public static void Save<T>(string section, T data)
    {
        lock (FileLock)
        {
            if (JsonFileStore.IsBroken(FileName)) return;

            var file = JsonFileStore.Load<Dictionary<string, JsonElement>>(FileName);
            file[section] = JsonSerializer.SerializeToElement(data);
            JsonFileStore.Save(FileName, file);
        }
    }
}
