using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Avalonia.Media;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Styles;

namespace StreamAssist.Components.Theme.Model;



// one named theme: the colors and numbers it changes from the app's defaults (only those, so a default changed in the
// app's files still reaches everything a theme leaves alone). Changed only through ThemeComponentModel's Try methods,
// which check the values first
public sealed class ThemeConfigModel : INamedConfig, IJsonOnDeserialized
{
    // private



    private string _name;
    private Dictionary<string, string> _colors; // palette key -> "#rrggbb"
    private Dictionary<string, double> _values; // style / spacing / component key -> number



    // public



    [JsonIgnore] public string Name => _name;



    // json



    [JsonInclude] [JsonPropertyName("name")]
    private string JsonName
    {
        get => _name;
        set => _name = value ?? "";
    }

    [JsonInclude] [JsonPropertyName("colors")]
    private Dictionary<string, string> JsonColors
    {
        get => _colors;
        set => _colors = value ?? [];
    }

    [JsonInclude] [JsonPropertyName("values")]
    private Dictionary<string, double> JsonValues
    {
        get => _values;
        set => _values = value ?? [];
    }



    // events



    public event Action? Changed; // anything saved changed
    public event Action<string>? ColorChanged; // a palette key (changed or reset)
    public event Action<string>? ValueChanged; // a number's key (changed or reset)



    // constructor



    public ThemeConfigModel()
    {
        _name = "";
        _colors = [];
        _values = [];
    }



    // json callbacks



    // drops what isn't (or is no longer) something the tab shows, or isn't a usable value
    public void OnDeserialized()
    {
        foreach (var key in _colors.Keys.ToList())
            if (!ThemePalette.Colors.Any(c => c.Key == key) || !Color.TryParse(_colors[key], out _)) _colors.Remove(key);

        foreach (var key in _values.Keys.ToList())
            if (SpacingRecipes.AllValues.FirstOrDefault(v => v.Key == key) is not { } info || !info.Accepts(_values[key])) _values.Remove(key);
    }



    // public api



    public bool TryGetColor(string key, out string color) => _colors.TryGetValue(key, out color!);
    public bool TryGetValue(string key, out double value) => _values.TryGetValue(key, out value);



    public void SetName(string name)
    {
        if (name == _name) return;

        _name = name;
        Changed?.Invoke();
    }



    // null takes it back to the default
    public void SetColor(string key, string? color)
    {
        if (color == null ? !_colors.Remove(key) : _colors.TryGetValue(key, out var old) && old == color) return;
        if (color != null) _colors[key] = color;

        ColorChanged?.Invoke(key);
        Changed?.Invoke();
    }



    public void SetValue(string key, double? value)
    {
        if (value == null ? !_values.Remove(key) : _values.TryGetValue(key, out var old) && old == value) return;
        if (value != null) _values[key] = value.Value;

        ValueChanged?.Invoke(key);
        Changed?.Invoke();
    }



    // disposal



    public void Dispose() { }
}
