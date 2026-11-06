using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Theme.Model;
using StreamAssist.Core.Mvvm;
using StreamAssist.Shell;
using StreamAssist.Styles;

namespace StreamAssist.Components.Theme.UI;



// the Themes tab: a row per palette color, then a row per base spacing value. Each box applies when it's left (Enter or
// clicking away), the app follows at once; a refused value is put back
public sealed partial class ThemeComponentViewModel : ViewModelBase
{
    // private



    private readonly ThemeComponentModel _model;
    private readonly List<ThemeColorRowViewModel> _colors;
    private readonly List<ThemeSpacingRowViewModel> _spacing;



    // public



    public IReadOnlyList<ThemeColorRowViewModel> Colors => _colors;
    public IReadOnlyList<ThemeSpacingRowViewModel> Spacing => _spacing;



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
        _colors = ThemePalette.Colors.Select(c => new ThemeColorRowViewModel(_model, c.Key, c.Label)).ToList();
        _spacing = SpacingRecipes.BaseValues.Select(b => new ThemeSpacingRowViewModel(_model, b.Key, b.Label)).ToList();

        SubscribeToModel();
    }



    // ui commands



    [RelayCommand] private void ResetColors() => _model.ResetAllColors();
    [RelayCommand] private void ResetSpacing() => _model.ResetAllSpacing();



    // model callbacks



    private void SubscribeToModel()
    {
        _model.ColorChanged += OnColorChanged;
        _model.SpacingChanged += OnSpacingChanged;
    }



    private void UnsubscribeFromModel()
    {
        _model.ColorChanged -= OnColorChanged;
        _model.SpacingChanged -= OnSpacingChanged;
    }



    private void OnColorChanged(string key) => _colors.FirstOrDefault(r => r.Key == key)?.Refresh();
    private void OnSpacingChanged(string key) => _spacing.FirstOrDefault(r => r.Key == key)?.Refresh();



    // disposal



    public override void Dispose() => UnsubscribeFromModel();
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



    // shows the model's value (after a change, or putting back a refused one)
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



// a base spacing value: its label, its number, Reset while it's changed
public sealed partial class ThemeSpacingRowViewModel : ViewModelBase, ICommitOnLeave
{
    private readonly ThemeComponentModel _model;

    [ObservableProperty] private string _input;
    [ObservableProperty] private bool _isChanged;

    public string Key { get; }
    public string Label { get; }



    public ThemeSpacingRowViewModel(ThemeComponentModel model, string key, string label)
    {
        _model = model;
        Key = key;
        Label = label;
        _input = "";

        Refresh();
    }



    public void Refresh()
    {
        Input = Text(_model.GetSpacing(Key));
        IsChanged = _model.IsSpacingChanged(Key);
    }



    public void Commit()
    {
        if (Input.Trim() != Text(_model.GetSpacing(Key))) _model.TrySetSpacing(Key, Input);
        Refresh();
    }



    [RelayCommand] private void Reset() => _model.ResetSpacing(Key);



    private static string Text(double value) => value.ToString(CultureInfo.InvariantCulture);



    public override void Dispose() { }
}
