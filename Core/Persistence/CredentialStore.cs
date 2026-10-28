using System.Collections.Generic;
using System.Text.Json;

namespace StreamAssist.Core.Persistence;



// every secret the app keeps (app credentials, login tokens, ...) in one file, Config/auth.json (git ignored)
// each service reads and writes its own section, e.g. "twitchApps", "twitchLogins", later "spotify"
public static class CredentialStore
{
    // private



    private const string FileName = "auth";
    private static readonly object FileLock = new(); // sections can be saved from different threads



    // public api



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



    // replaces one section, leaving the others as they are
    public static void Save<T>(string section, T data)
    {
        lock (FileLock)
        {
            var file = JsonFileStore.Load<Dictionary<string, JsonElement>>(FileName);
            file[section] = JsonSerializer.SerializeToElement(data);
            JsonFileStore.Save(FileName, file);
        }
    }
}
