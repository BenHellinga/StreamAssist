using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;
using Avalonia.Media;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Core.Mvvm;
using StreamAssist.Styles;

namespace StreamAssist.Components.Theme.Model;



// the app's look, app-wide: named themes (configs, like the other tabs'), the one in use changing the palette colors
// (ThemePalette) and the numbers (SpacingRecipes: styles, spacing, components) from the defaults the app ships with
// (the resource files, handed over by ThemeManager at startup). With no theme the defaults are used and nothing can be
// changed (the tab says to make one). Saved to Config/theme.json
public class ThemeComponentModel : ModelBase
{
    // private



    private readonly ConfigSet<ThemeConfigModel> _configs;
    private Dictionary<string, string> _defaultColors;
    private Dictionary<string, double> _defaultValues;
    private ThemeConfigModel? _subscribed; // the theme in use, whose changes are passed on



    // public



    [JsonIgnore] public ConfigSet<ThemeConfigModel> Configs => _configs;



    // json



    [JsonInclude] [JsonPropertyName("configs")]
    private List<ThemeConfigModel> JsonConfigs
    {
        get => _configs.Items.ToList();
        set
        {
            _configs.LoadItems(value);
            FollowSelected();
        }
    }

    [JsonInclude] [JsonPropertyName("selectedConfig")]
    private string? JsonSelectedConfig
    {
        get => _configs.SelectedName;
        set
        {
            _configs.LoadSelected(value);
            FollowSelected();
        }
    }



    // events



    public event Action<string>? ColorChanged; // a palette key's color (changed, reset, or another theme chosen)
    public event Action<string>? ValueChanged; // a number (changed, reset, or another theme chosen)
    public event Action? Changed; // something saved changed



    // constructor



    public ThemeComponentModel()
    {
        _configs = new("theme", () => new ThemeConfigModel());
        _defaultColors = [];
        _defaultValues = [];

        _configs.Changed += OnConfigsChanged;
        _configs.SelectedChanged += OnSelectedChanged;
    }



    // public api



    // what the app ships with, read from its resources at startup (by ThemeManager, before anything is changed)
    public void SetDefaults(IReadOnlyDictionary<string, string> colors, IReadOnlyDictionary<string, double> values)
    {
        _defaultColors = new(colors);
        _defaultValues = new(values);
    }



    public string GetColor(string key) =>
        _configs.Selected != null && _configs.Selected.TryGetColor(key, out var color) ? color : _defaultColors.GetValueOrDefault(key, "#000000");

    public bool IsColorChanged(string key) => _configs.Selected?.TryGetColor(key, out _) == true;

    public double GetValue(string key) =>
        _configs.Selected != null && _configs.Selected.TryGetValue(key, out var value) ? value : _defaultValues.GetValueOrDefault(key);

    public bool IsValueChanged(string key) => _configs.Selected?.TryGetValue(key, out _) == true;



    // a color as text: #rgb, #rrggbb, #aarrggbb or a name (red); the default again counts as unchanged
    public bool TrySetColor(string key, string text)
    {
        if (_configs.Selected is not { } theme)
        {
            AppLog.WriteError("can't set theme color, no theme");
            return false;
        }

        if (!ThemePalette.Colors.Any(c => c.Key == key))
        {
            AppLog.WriteError($"can't set theme color, no such color ({key})");
            return false;
        }

        if (!Color.TryParse(text.Trim(), out var color))
        {
            AppLog.WriteError($"can't set theme color, not a color ({text.Trim()})");
            return false;
        }

        var value = ToText(color);
        theme.SetColor(key, value == _defaultColors.GetValueOrDefault(key) ? null : value);
        return true;
    }



    // a number in the value's range, as text (what the tab's boxes hold); the default again counts as unchanged
    public bool TrySetValue(string key, string text)
    {
        if (_configs.Selected is not { } theme)
        {
            AppLog.WriteError("can't set theme value, no theme");
            return false;
        }

        if (SpacingRecipes.AllValues.FirstOrDefault(v => v.Key == key) is not { } info)
        {
            AppLog.WriteError($"can't set theme value, no such value ({key})");
            return false;
        }

        if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !info.Accepts(value))
        {
            AppLog.WriteError($"can't set {info.Label.ToLowerInvariant()}, must be {info.Min} to {info.Max} ({text.Trim()})");
            return false;
        }

        theme.SetValue(key, _defaultValues.TryGetValue(key, out var def) && value == def ? null : value);
        return true;
    }



    public void ResetColor(string key) => _configs.Selected?.SetColor(key, null);
    public void ResetValue(string key) => _configs.Selected?.SetValue(key, null);



    public void ResetAllColors()
    {
        foreach (var color in ThemePalette.Colors) ResetColor(color.Key);
    }



    public void ResetAll(IEnumerable<ThemeValue> values)
    {
        foreach (var value in values) ResetValue(value.Key);
    }



    // "#rrggbb", or "#aarrggbb" when it isn't fully opaque
    public static string ToText(Color color) =>
        color.A == 255 ? $"#{color.R:x2}{color.G:x2}{color.B:x2}" : $"#{color.A:x2}{color.R:x2}{color.G:x2}{color.B:x2}";



    // config callbacks



    private void OnConfigsChanged() => Changed?.Invoke();



    // another theme (or none): everything may look different
    private void OnSelectedChanged(ThemeConfigModel? oldTheme, ThemeConfigModel? newTheme)
    {
        FollowSelected();

        foreach (var color in ThemePalette.Colors) ColorChanged?.Invoke(color.Key);
        foreach (var value in SpacingRecipes.AllValues) ValueChanged?.Invoke(value.Key);
    }



    private void OnThemeColorChanged(string key) => ColorChanged?.Invoke(key);
    private void OnThemeValueChanged(string key) => ValueChanged?.Invoke(key);



    // helpers



    // passes on the changes of the theme in use (the one kept, not re-read later, so it's always the one unsubscribed)
    private void FollowSelected()
    {
        if (_subscribed == _configs.Selected) return;

        if (_subscribed != null)
        {
            _subscribed.ColorChanged -= OnThemeColorChanged;
            _subscribed.ValueChanged -= OnThemeValueChanged;
        }

        _subscribed = _configs.Selected;
        if (_subscribed != null)
        {
            _subscribed.ColorChanged += OnThemeColorChanged;
            _subscribed.ValueChanged += OnThemeValueChanged;
        }
    }



    // disposal



    public override void Dispose()
    {
        if (_subscribed != null)
        {
            _subscribed.ColorChanged -= OnThemeColorChanged;
            _subscribed.ValueChanged -= OnThemeValueChanged;
            _subscribed = null;
        }

        _configs.Changed -= OnConfigsChanged;
        _configs.SelectedChanged -= OnSelectedChanged;
        _configs.Dispose();
    }
}
