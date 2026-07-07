using System.IO;
using System.Text.Json;

namespace StreamAssist.Components.Config;

/// <summary>
/// Minimal helper for loading/saving a feature's settings as a single JSON file.
/// Each feature defines its own plain data class (e.g. KeyboardListData) with
/// whatever shape it needs — there's no shared base class, since different
/// features' save data isn't guaranteed to look alike.
/// </summary>
public static class JsonFileStore
{
    private static readonly string RootDir = "C:\\Users\\benhe\\OneDrive\\Documents\\Code Projects\\StreamAssist\\Config";
    // private static readonly string RootDir = Path.Combine(
    //     Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    //     "StreamAssist");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };



    /// <summary>Load a feature's JSON file, or a fresh default instance if it doesn't exist / fails to parse.</summary>
    public static T Load<T>(string featureFileName) where T : class, new()
    {
        var path = GetPath(featureFileName);

        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions) ?? new T();
        }
        catch
        {
            // Corrupt or unreadable file — fall back to defaults rather than crash.
        }

        return new T();
    }



    /// <summary>Persist a feature's data to its JSON file.</summary>
    public static void Save<T>(string featureFileName, T data)
    {
        Directory.CreateDirectory(RootDir);
        File.WriteAllText(GetPath(featureFileName), JsonSerializer.Serialize(data, JsonOptions));
    }



    private static string GetPath(string featureFileName) =>
        Path.Combine(RootDir, $"{featureFileName}.json");
}