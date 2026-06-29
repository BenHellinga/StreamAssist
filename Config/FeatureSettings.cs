using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamAssist.Config;

/// <summary>
/// Wraps a feature's JSON file on disk. The file looks like:
///
/// {
///   "activeConfig": "Default",
///   "configs": {
///     "Default":      { ...feature settings... },
///     "StreamingDay": { ...feature settings... }
///   }
/// }
///
/// Usage — create a concrete subclass per feature:
///
///   public class KeyboardFeatureSettings : FeatureSettings&lt;KeyboardConfig&gt;
///   {
///       public KeyboardFeatureSettings() : base("keyboard") { }
///   }
///
/// Then call Load() at startup, read/write Active, call Save() after changes.
/// Call CreateConfig / SwitchConfig / DeleteConfig to manage named configs.
/// </summary>
public class FeatureSettings<TConfig> where TConfig : class, new()
{
    // ── Paths ─────────────────────────────────────────────────────────────

    private static readonly string RootDir = "C:\\Users\\benhe\\OneDrive\\Documents\\Code Projects\\StreamAssist\\Config";
    // private static readonly string RootDir = Path.Combine(
    //     Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    //     "StreamAssist");

    private static readonly JsonSerializerOptions JsonOptions =
        new() { WriteIndented = true };

    private readonly string _filePath;

    // ── Internal envelope stored in JSON ─────────────────────────────────

    private Envelope _envelope = new();

    private class Envelope
    {
        [JsonPropertyName("activeConfig")]
        public string ActiveConfig { get; set; } = "Default";

        [JsonPropertyName("configs")]
        public Dictionary<string, TConfig> Configs { get; set; } = new()
        {
            ["Default"] = new TConfig()
        };
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>The name of the currently active config.</summary>
    public string ActiveConfigName => _envelope.ActiveConfig;

    /// <summary>The settings object for the active config. Mutate this directly, then call Save().</summary>
    public TConfig Active => _envelope.Configs[_envelope.ActiveConfig];

    /// <summary>All config names in this feature file, alphabetically.</summary>
    public IReadOnlyList<string> ConfigNames =>
        _envelope.Configs.Keys.OrderBy(k => k).ToList();

    protected FeatureSettings(string featureFileName)
    {
        _filePath = Path.Combine(RootDir, $"{featureFileName}.json");
    }

    /// <summary>
    /// Load from disk. Safe to call on a fresh install — creates Default config automatically.
    /// Call once at app startup before constructing ViewModels.
    /// </summary>
    public void Load()
    {
        Directory.CreateDirectory(RootDir);

        try
        {
            if (File.Exists(_filePath))
                _envelope = JsonSerializer.Deserialize<Envelope>(
                    File.ReadAllText(_filePath), JsonOptions) ?? new Envelope();
        }
        catch { _envelope = new Envelope(); }

        // Ensure at least a Default config exists
        if (!_envelope.Configs.ContainsKey("Default"))
            _envelope.Configs["Default"] = new TConfig();

        // If the saved active config was deleted, fall back to Default
        if (!_envelope.Configs.ContainsKey(_envelope.ActiveConfig))
            _envelope.ActiveConfig = "Default";
    }

    /// <summary>Persist the current state to disk.</summary>
    public void Save()
    {
        Directory.CreateDirectory(RootDir);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(_envelope, JsonOptions));
    }

    /// <summary>
    /// Switch to a different config by name. Saves first, then switches.
    /// Returns false if the name doesn't exist.
    /// </summary>
    public bool SwitchConfig(string name)
    {
        if (!_envelope.Configs.ContainsKey(name)) return false;
        Save();
        _envelope.ActiveConfig = name;
        Save();
        return true;
    }

    /// <summary>
    /// Create a new config with the given name.
    /// If copyFromActive is true, duplicates the active config's values.
    /// Returns false if the name is already taken.
    /// </summary>
    public bool CreateConfig(string name, bool copyFromActive = false)
    {
        if (_envelope.Configs.ContainsKey(name)) return false;

        _envelope.Configs[name] = copyFromActive
            ? DeepClone(Active)
            : new TConfig();

        Save();
        return true;
    }

    /// <summary>
    /// Delete a config by name. Cannot delete "Default" or the active config.
    /// Returns false if deletion is not allowed.
    /// </summary>
    public bool DeleteConfig(string name)
    {
        if (name == "Default") return false;
        if (!_envelope.Configs.ContainsKey(name)) return false;

        _envelope.Configs.Remove(name);

        if (_envelope.ActiveConfig == name)
            _envelope.ActiveConfig = "Default";

        Save();
        return true;
    }

    /// <summary>
    /// Rename a config. Cannot rename "Default".
    /// Returns false if the old name doesn't exist or the new name is taken.
    /// </summary>
    public bool RenameConfig(string oldName, string newName)
    {
        if (oldName == "Default") return false;
        if (!_envelope.Configs.ContainsKey(oldName)) return false;
        if (_envelope.Configs.ContainsKey(newName)) return false;

        _envelope.Configs[newName] = _envelope.Configs[oldName];
        _envelope.Configs.Remove(oldName);

        if (_envelope.ActiveConfig == oldName)
            _envelope.ActiveConfig = newName;

        Save();
        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static TConfig DeepClone(TConfig value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        return JsonSerializer.Deserialize<TConfig>(json, JsonOptions) ?? new TConfig();
    }
}
