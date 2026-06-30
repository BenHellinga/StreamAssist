using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamAssist.Config;

/// <summary>Marker type for features that don't need extra root-level data.</summary>
public sealed class NoRootData;

/// <summary>
/// Convenience version of <see cref="FeatureSettings{TConfig, TRootData}"/> for the
/// common case of a feature that only needs per-config data, with nothing extra at
/// the root. See that class for the full docs on the on-disk layout and API.
/// </summary>
public class FeatureSettings<TConfig> : FeatureSettings<TConfig, NoRootData>
    where TConfig : class, new()
{
    protected FeatureSettings(string featureFileName) : base(featureFileName) { }
}

/// <summary>
/// Wraps a feature's JSON file on disk. The file looks like:
///
/// {
///   "configs": {
///     "Default":      { ...feature settings... },
///     "StreamingDay": { ...feature settings... }
///   },
///   "rootData": { ...feature-wide settings that aren't scoped to one config... }
/// }
///
/// "activeConfig" only appears if a feature actually calls SwitchConfig — for
/// features (like Keyboard) where every config is just shared, named data with
/// no single "active" one, it's simply omitted.
///
/// Usage — create a concrete subclass per feature:
///
///   public class KeyboardFeatureSettings : FeatureSettings&lt;KeyboardConfig, KeyboardRootData&gt;
///   {
///       public KeyboardFeatureSettings() : base("keyboard") { }
///   }
///
/// Then call Load() at startup, read/write Active or RootData, call Save() after
/// changes. Call CreateConfig / SwitchConfig / DeleteConfig to manage named configs;
/// subscribe to ConfigsChanged if multiple consumers need to stay in sync.
/// </summary>
public class FeatureSettings<TConfig, TRootData>
    where TConfig : class, new()
    where TRootData : class, new()
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
        // Nullable + omitted when null: most features (e.g. Keyboard) never
        // call SwitchConfig, so this shouldn't clutter their JSON.
        [JsonPropertyName("activeConfig")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ActiveConfig { get; set; } = null;

        [JsonPropertyName("configs")]
        public Dictionary<string, TConfig> Configs { get; set; } = new()
        {
            ["Default"] = new TConfig()
        };

        [JsonPropertyName("rootData")]
        public TRootData RootData { get; set; } = new();
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>The name of the currently active config (falls back to "Default" if unset).</summary>
    public string ActiveConfigName => _envelope.ActiveConfig ?? "Default";

    /// <summary>The settings object for the active config. Mutate this directly, then call Save().</summary>
    public TConfig Active => _envelope.Configs[ActiveConfigName];

    /// <summary>All config names in this feature file, alphabetically.</summary>
    public IReadOnlyList<string> ConfigNames =>
        _envelope.Configs.Keys.OrderBy(k => k).ToList();

    /// <summary>Feature-wide data that isn't scoped to any single config (e.g. UI layout state). Mutate directly, then call Save().</summary>
    public TRootData RootData => _envelope.RootData;

    /// <summary>Raised after CreateConfig/DeleteConfig/RenameConfig actually changes something — lets multiple independent consumers (e.g. several keyboard slots) stay in sync with each other.</summary>
    public event Action? ConfigsChanged;

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

        // If the saved active config was deleted, clear it so it falls back
        // to Default via ActiveConfigName rather than persisting a stale value.
        if (_envelope.ActiveConfig is not null && !_envelope.Configs.ContainsKey(_envelope.ActiveConfig))
            _envelope.ActiveConfig = null;

        _envelope.RootData ??= new TRootData();
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
        ConfigsChanged?.Invoke();
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
            _envelope.ActiveConfig = null;

        Save();
        ConfigsChanged?.Invoke();
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
        ConfigsChanged?.Invoke();
        return true;
    }

    /// <summary>Looks up a config by name without throwing. Use this for callers that aren't sure the config still exists.</summary>
    public bool TryGetConfig(string name, out TConfig config) =>
        _envelope.Configs.TryGetValue(name, out config!);

    /// <summary>Looks up a config by name. Throws if it doesn't exist — prefer TryGetConfig for untrusted names.</summary>
    public TConfig GetConfig(string name)
    {
        if (!_envelope.Configs.TryGetValue(name, out var config))
            throw new KeyNotFoundException($"No config named '{name}'.");
        return config;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static TConfig DeepClone(TConfig value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        return JsonSerializer.Deserialize<TConfig>(json, JsonOptions) ?? new TConfig();
    }
}