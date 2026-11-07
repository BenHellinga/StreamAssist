using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Components.Theme.Model;
using StreamAssist.Core.Mvvm;
using StreamAssist.Shell;
using StreamAssist.Styles;

namespace StreamAssist.Components.Theme.UI;



// the Themes tab: the config bar (named themes), then a row per palette color, per style value, per spacing value and
// per component value (a subsection per component). Each box applies when it's left (Enter or clicking away), the app
// follows at once; a refused value is put back. With no theme, the page says to make one
public sealed partial class ThemeComponentViewModel : ViewModelBase
{
    // private



    private readonly ThemeComponentModel _model;
    private readonly ConfigBarViewModel _configBar;
    private readonly NoConfigViewModel _noConfig;
    private readonly List<ThemeColorRowViewModel> _colors;
    private readonly List<ThemeValueRowViewModel> _styles;
    private readonly List<ThemeValueRowViewModel> _spacing;
    private readonly List<ThemeGroupViewModel> _components;

    [ObservableProperty] private bool _hasConfig;



    // public



    public ConfigBarViewModel ConfigBar => _configBar;
    public NoConfigViewModel NoConfig => _noConfig;
    public IReadOnlyList<ThemeColorRowViewModel> Colors => _colors;
    public IReadOnlyList<ThemeValueRowViewModel> Styles => _styles;
    public IReadOnlyList<ThemeValueRowViewModel> Spacing => _spacing;
    public IReadOnlyList<ThemeGroupViewModel> Components => _components;



    // static api



    public static ThemeComponentViewModel ActivateTab(ThemeComponentModel model, PanelViewModel sidePanel, PanelViewModel bottomPanel)
    {
        sidePanel.TransferContent(null);
        bottomPanel.TransferContent(null);

        return new(model);
    }



    // constructor



    public ThemeComponentViewModel(ThemeComponentModel model)
    {
        _model = model;
        _configBar = new(_model.Configs);
        _noConfig = new();
        _colors = ThemePalette.Colors.Select(c => new ThemeColorRowViewModel(_model, c.Key, c.Label)).ToList();
        _styles = Rows(SpacingRecipes.StyleValues);
        _spacing = Rows(SpacingRecipes.BaseValues);
        _components = SpacingRecipes.ComponentValues.GroupBy(v => v.Group ?? "Other")
            .Select(g => new ThemeGroupViewModel(g.Key, Rows(g), () => _model.ResetAll(g))).ToList();
        _hasConfig = _model.Configs.Selected != null;

        SubscribeToModel();
    }



    // ui commands



    [RelayCommand] private void ResetColors() => _model.ResetAllColors();
    [RelayCommand] private void ResetStyles() => _model.ResetAll(SpacingRecipes.StyleValues);
    [RelayCommand] private void ResetSpacing() => _model.ResetAll(SpacingRecipes.BaseValues);



    // model callbacks



    private void SubscribeToModel()
    {
        _model.ColorChanged += OnColorChanged;
        _model.ValueChanged += OnValueChanged;
        _model.Configs.ListChanged += OnConfigsListChanged;
    }



    private void UnsubscribeFromModel()
    {
        _model.ColorChanged -= OnColorChanged;
        _model.ValueChanged -= OnValueChanged;
        _model.Configs.ListChanged -= OnConfigsListChanged;
    }



    private void OnColorChanged(string key) => _colors.FirstOrDefault(r => r.Key == key)?.Refresh();
    private void OnValueChanged(string key) => AllValueRows().FirstOrDefault(r => r.Key == key)?.Refresh();
    private void OnConfigsListChanged() => HasConfig = _model.Configs.Selected != null;



    // helpers



    private List<ThemeValueRowViewModel> Rows(IEnumerable<ThemeValue> values) =>
        values.Select(v => new ThemeValueRowViewModel(_model, v.Key, v.Label)).ToList();

    private IEnumerable<ThemeValueRowViewModel> AllValueRows() => _styles.Concat(_spacing).Concat(_components.SelectMany(g => g.Rows));



    // disposal



    public override void Dispose()
    {
        UnsubscribeFromModel();
        _configBar.Dispose();
    }
}



// a tab's own section (e.g. Input, Widgets): its name, its values' rows, and resetting them
public sealed partial class ThemeGroupViewModel(string name, IReadOnlyList<ThemeValueRowViewModel> rows, Action reset) : ViewModelBase
{
    public string Name { get; } = name;
    public IReadOnlyList<ThemeValueRowViewModel> Rows { get; } = rows;
    public string ResetText => $"Reset all {Name.ToLowerInvariant()}";

    [RelayCommand] private void OnResetPressed() => reset();

    public override void Dispose() { }
}



// a palette color: its label, a swatch, its value as text, Reset while it's changed
public sealed partial class ThemeColorRowViewModel : ViewModelBase, ICommitOnLeave
{
    private readonly ThemeComponentModel _model;

    [ObservableProperty] private string _input;
    [ObservableProperty] private IBrush _swatch;
    [ObservableProperty] private bool _isChanged;

    public string Key { get; }
    public string Label { get; }



    public ThemeColorRowViewModel(ThemeComponentModel model, string key, string label)
    {
        _model = model;
        Key = key;
        Label = label;
        _input = "";
        _swatch = Brushes.Transparent;

        Refresh();
    }



    // shows the model's value (after a change, another theme, or putting back a refused one)
    public void Refresh()
    {
        Input = _model.GetColor(Key);
        Swatch = Color.TryParse(Input, out var color) ? new SolidColorBrush(color) : Brushes.Transparent;
        IsChanged = _model.IsColorChanged(Key);
    }



    public void Commit()
    {
        if (Input.Trim() != _model.GetColor(Key)) _model.TrySetColor(Key, Input);
        Refresh();
    }



    [RelayCommand] private void Reset() => _model.ResetColor(Key);



    public override void Dispose() { }
}



// a number (a style, spacing or component value): its label, its number, Reset while it's changed
public sealed partial class ThemeValueRowViewModel : ViewModelBase, ICommitOnLeave
{
    private readonly ThemeComponentModel _model;

    [ObservableProperty] private string _input;
    [ObservableProperty] private bool _isChanged;

    public string Key { get; }
    public string Label { get; }



    public ThemeValueRowViewModel(ThemeComponentModel model, string key, string label)
    {
        _model = model;
        Key = key;
        Label = label;
        _input = "";

        Refresh();
    }



    public void Refresh()
    {
        Input = Text(_model.GetValue(Key));
        IsChanged = _model.IsValueChanged(Key);
    }



    public void Commit()
    {
        if (Input.Trim() != Text(_model.GetValue(Key))) _model.TrySetValue(Key, Input);
        Refresh();
    }



    [RelayCommand] private void Reset() => _model.ResetValue(Key);



    private static string Text(double value) => value.ToString(CultureInfo.InvariantCulture);



    public override void Dispose() { }
}
