using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;
using Avalonia.Media;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Core.Mvvm;
using StreamAssist.Styles;

namespace StreamAssist.Components.Theme.Model;



// the app's look, app-wide: the palette colors (ThemePalette) and the base spacing values (SpacingRecipes.BaseValues),
// each either changed by the user (saved to Config/theme.json) or the default the app ships with (Brushes.axaml /
// Spacing.axaml, handed over by ThemeManager at startup). Only changes are saved, so a default changed in the app's
// files still reaches everything the user left alone
public class ThemeComponentModel : ModelBase, IJsonOnDeserialized
{
    // constants



    public const double MaxSpacing = 100;



    // private



    private Dictionary<string, string> _colors; // palette key -> "#rrggbb", only the changed ones
    private Dictionary<string, double> _spacing; // base value key -> number, only the changed ones
    private Dictionary<string, string> _defaultColors;
    private Dictionary<string, double> _defaultSpacing;



    // json



    [JsonInclude] [JsonPropertyName("colors")]
    private Dictionary<string, string> JsonColors
    {
        get => _colors;
        set => _colors = value ?? [];
    }

    [JsonInclude] [JsonPropertyName("spacing")]
    private Dictionary<string, double> JsonSpacing
    {
        get => _spacing;
        set => _spacing = value ?? [];
    }



    // events



    public event Action<string>? ColorChanged; // a palette key's color (changed or reset)
    public event Action<string>? SpacingChanged; // a base value's number (changed or reset)
    public event Action? Changed; // something saved changed



    // constructor



    public ThemeComponentModel()
    {
        _colors = [];
        _spacing = [];
        _defaultColors = [];
        _defaultSpacing = [];
    }



    // json callbacks



    // drops what isn't (or is no longer) something the tab shows, or isn't a usable value
    public void OnDeserialized()
    {
        foreach (var key in _colors.Keys.ToList())
            if (!IsColorKey(key) || !Color.TryParse(_colors[key], out _)) _colors.Remove(key);

        foreach (var key in _spacing.Keys.ToList())
            if (!IsSpacingKey(key) || _spacing[key] is < 0 or > MaxSpacing || double.IsNaN(_spacing[key])) _spacing.Remove(key);
    }



    // public api



    // what the app ships with, read from its resources at startup (by ThemeManager, before anything is changed)
    public void SetDefaults(IReadOnlyDictionary<string, string> colors, IReadOnlyDictionary<string, double> spacing)
    {
        _defaultColors = new(colors);
        _defaultSpacing = new(spacing);
    }



    public string GetColor(string key) => _colors.TryGetValue(key, out var color) ? color : _defaultColors.GetValueOrDefault(key, "#000000");
    public bool IsColorChanged(string key) => _colors.ContainsKey(key);
    public double GetSpacing(string key) => _spacing.TryGetValue(key, out var value) ? value : _defaultSpacing.GetValueOrDefault(key);
    public bool IsSpacingChanged(string key) => _spacing.ContainsKey(key);



    // a color as text: #rgb, #rrggbb, #aarrggbb or a name (red)
    public bool TrySetColor(string key, string text)
    {
        if (!IsColorKey(key))
        {
            AppLog.WriteError($"can't set theme color, no such color ({key})");
            return false;
        }

        if (!Color.TryParse(text.Trim(), out var color))
        {
            AppLog.WriteError($"can't set theme color, not a color ({text.Trim()})");
            return false;
        }

        SetColor(key, ToText(color));
        return true;
    }



    // a number from 0 to MaxSpacing, as text (what the tab's boxes hold)
    public bool TrySetSpacing(string key, string text)
    {
        if (!IsSpacingKey(key))
        {
            AppLog.WriteError($"can't set theme spacing, no such value ({key})");
            return false;
        }

        if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || value is < 0 or > MaxSpacing)
        {
            AppLog.WriteError($"can't set theme spacing, must be 0 to {MaxSpacing} ({text.Trim()})");
            return false;
        }

        SetSpacing(key, value);
        return true;
    }



    public void ResetColor(string key)
    {
        if (!_colors.Remove(key)) return;

        ColorChanged?.Invoke(key);
        Changed?.Invoke();
    }



    public void ResetSpacing(string key)
    {
        if (!_spacing.Remove(key)) return;

        SpacingChanged?.Invoke(key);
        Changed?.Invoke();
    }



    public void ResetAllColors()
    {
        foreach (var key in _colors.Keys.ToList()) ResetColor(key);
    }



    public void ResetAllSpacing()
    {
        foreach (var key in _spacing.Keys.ToList()) ResetSpacing(key);
    }



    // "#rrggbb", or "#aarrggbb" when it isn't fully opaque
    public static string ToText(Color color) =>
        color.A == 255 ? $"#{color.R:x2}{color.G:x2}{color.B:x2}" : $"#{color.A:x2}{color.R:x2}{color.G:x2}{color.B:x2}";



    // private



    // the default again counts as unchanged (so it follows the app's files from then on)
    private void SetColor(string key, string color)
    {
        if (color == GetColor(key)) return;

        if (color == _defaultColors.GetValueOrDefault(key)) _colors.Remove(key);
        else _colors[key] = color;

        ColorChanged?.Invoke(key);
        Changed?.Invoke();
    }



    private void SetSpacing(string key, double value)
    {
        if (value == GetSpacing(key)) return;

        if (_defaultSpacing.TryGetValue(key, out var def) && value == def) _spacing.Remove(key);
        else _spacing[key] = value;

        SpacingChanged?.Invoke(key);
        Changed?.Invoke();
    }



    private static bool IsColorKey(string key) => ThemePalette.Colors.Any(c => c.Key == key);
    private static bool IsSpacingKey(string key) => SpacingRecipes.BaseValues.Any(b => b.Key == key);



    // disposal



    public override void Dispose() { }
}
