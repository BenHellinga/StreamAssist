using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using StreamAssist.Components.Theme.Model;
using StreamAssist.Core.Persistence;
using StreamAssist.Styles;

namespace StreamAssist.Components.Theme.Background;



// puts the theme on the app: reads the shipped defaults from its resources once, then applies the user's changes at
// startup and whenever one changes, and saves them (Config/theme.json).
// A color sets every brush it paints in place (ThemePalette); a spacing value replaces its
// resource at the app level (found before Spacing.axaml's), which views follow through DynamicResource and {s:Gap}
public class ThemeManager : IDisposable
{
    // private



    public const string FileName = "theme";
    private readonly ThemeComponentModel _model;



    // constructor



    public ThemeManager(ThemeComponentModel model)
    {
        _model = model;
        _model.SetDefaults(ReadDefaultColors(), ReadDefaultSpacing());

        foreach (var color in ThemePalette.Colors) ApplyColor(color.Key);
        foreach (var value in SpacingRecipes.BaseValues) ApplySpacing(value.Key);

        _model.ColorChanged += ApplyColor;
        _model.SpacingChanged += ApplySpacing;
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



    private void ApplySpacing(string key)
    {
        var app = Application.Current;
        if (app == null) return;

        if (_model.IsSpacingChanged(key)) app.Resources[key] = _model.GetSpacing(key);
        else app.Resources.Remove(key); // back to Spacing.axaml's
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



    private static Dictionary<string, double> ReadDefaultSpacing()
    {
        var spacing = new Dictionary<string, double>();
        foreach (var (key, _, _) in SpacingRecipes.BaseValues)
            if (Application.Current?.TryFindResource(key, out var found) == true && found is double value)
                spacing[key] = value;
        return spacing;
    }



    // disposal



    public void Dispose()
    {
        _model.ColorChanged -= ApplyColor;
        _model.SpacingChanged -= ApplySpacing;
        _model.Changed -= OnModelChanged;
    }
}
