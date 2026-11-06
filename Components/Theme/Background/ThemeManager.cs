using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using StreamAssist.Components.Theme.Model;
using StreamAssist.Core.Persistence;
using StreamAssist.Styles;

namespace StreamAssist.Components.Theme.Background;



// puts the theme on the app: reads the shipped defaults from its resources once, then applies the user's changes at
// startup and whenever one changes, and saves them (Config/theme.json).
// A color sets every brush it paints in place (ThemePalette); a number replaces its resource (and the ones that always
// follow it) at the app level, found before the resource files', which views follow through DynamicResource and {s:Gap}
public class ThemeManager : IDisposable
{
    // private



    public const string FileName = "theme";
    private readonly ThemeComponentModel _model;



    // constructor



    public ThemeManager(ThemeComponentModel model)
    {
        _model = model;
        _model.SetDefaults(ReadDefaultColors(), ReadDefaultValues());

        foreach (var color in ThemePalette.Colors) ApplyColor(color.Key);
        foreach (var value in SpacingRecipes.AllValues) ApplyValue(value.Key);

        _model.ColorChanged += ApplyColor;
        _model.ValueChanged += ApplyValue;
        _model.Changed += OnModelChanged;
    }



    // public api



    public static ThemeComponentModel Load() => JsonFileStore.LoadOrReset<ThemeComponentModel>(FileName);



    // model callbacks



    private void ApplyColor(string key)
    {
        var app = Application.Current;
        if (app == null || !Color.TryParse(_model.GetColor(key), out var color)) return;

        // only the brushes: the palette's colors are read by them (once, when loaded) and nothing else
        foreach (var (paletteKey, _, brushes) in ThemePalette.Colors)
        {
            if (paletteKey != key) continue;
            foreach (var brushKey in brushes)
                if (app.TryFindResource(brushKey, out var found) && found is SolidColorBrush brush) brush.Color = color;
        }
    }



    private void ApplyValue(string key)
    {
        var app = Application.Current;
        var info = SpacingRecipes.AllValues.FirstOrDefault(v => v.Key == key);
        if (app == null || info == null) return;

        foreach (var resource in new[] { key }.Concat(info.AlsoSets ?? []))
        {
            if (_model.IsValueChanged(key)) app.Resources[resource] = _model.GetValue(key);
            else app.Resources.Remove(resource); // back to the resource file's
        }
    }



    private void OnModelChanged() => JsonFileStore.Save(FileName, _model);



    // helpers



    private static Dictionary<string, string> ReadDefaultColors()
    {
        var colors = new Dictionary<string, string>();
        foreach (var (key, _, _) in ThemePalette.Colors)
            if (Application.Current?.TryFindResource(key, out var found) == true && found is Color color)
                colors[key] = ThemeComponentModel.ToText(color);
        return colors;
    }



    private static Dictionary<string, double> ReadDefaultValues()
    {
        var values = new Dictionary<string, double>();
        foreach (var value in SpacingRecipes.AllValues)
            if (Application.Current?.TryFindResource(value.Key, out var found) == true && found is double number)
                values[value.Key] = number;
        return values;
    }



    // disposal



    public void Dispose()
    {
        _model.ColorChanged -= ApplyColor;
        _model.ValueChanged -= ApplyValue;
        _model.Changed -= OnModelChanged;
    }
}
