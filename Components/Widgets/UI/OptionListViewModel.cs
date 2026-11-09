using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Widgets.Model;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Widgets.UI;



// what's dragged when an option's handle is pulled: the option and the list it's in (only that list takes it)
public sealed record WidgetOptionDragPayload(WidgetOptionList List, WidgetOption Option);



// a widget's options in its box (a wheel's options, a plinko board's buckets): a row each, the empty last row adds one.
// Dragging a row's handle onto another row moves it before that one (the order is the order they're drawn in)
public sealed class OptionListViewModel : ViewModelBase, IDroppable
{
    // private



    private readonly WidgetOptionList _list;



    // public



    public ObservableCollection<OptionRowViewModel> Rows { get; }
    public string Placeholder { get; } // the empty row's, e.g. "New option…"



    // constructor



    public OptionListViewModel(WidgetOptionList list, string placeholder)
    {
        _list = list;
        Placeholder = placeholder;
        Rows = [];
        SyncRows();

        _list.Changed += SyncRows;
    }



    // drag drop: only reordering this list's own options



    public bool CanAcceptDropPayload(object payload) => payload is WidgetOptionDragPayload dragged && dragged.List == _list;



    public bool PassesOn(object payload) => !CanAcceptDropPayload(payload);



    // zoneContext is the row it was dropped on, it goes before that one
    public void OnDroppedOn(object payload, object? zoneContext)
    {
        if (payload is not WidgetOptionDragPayload dragged || zoneContext is not OptionRowViewModel before) return;
        _list.TryMove(dragged.Option, Rows.IndexOf(before));
    }



    // helpers



    // the rows follow the options, keeping the view model of each option that's still there (so the box being typed
    // in stays the same box)
    private void SyncRows()
    {
        var options = _list.Items;
        foreach (var gone in Rows.Where(r => !options.Contains(r.Option)).ToList())
        {
            Rows.Remove(gone);
            gone.Dispose();
        }

        for (var i = 0; i < options.Count; i++)
        {
            var at = Rows.ToList().FindIndex(r => r.Option == options[i]);
            if (at == i) continue;
            if (at >= 0) Rows.Move(at, i);
            else Rows.Insert(i, new OptionRowViewModel(_list, options[i], Placeholder));
        }
    }



    // disposal



    public override void Dispose()
    {
        _list.Changed -= SyncRows;
        foreach (var row in Rows) row.Dispose();
    }
}



// one option's row: its text (typing in the empty last row adds an option, emptying one removes it), its weight, its
// colour (the swatch opens the palette and a hex box) and its drag handle. A typed weight applies when its box is left,
// a colour as soon as it's a whole one
public sealed partial class OptionRowViewModel : ViewModelBase, ICommitOnLeave, IDraggable
{
    // private



    private readonly WidgetOptionList _list;
    private readonly WidgetOption _option;

    [ObservableProperty] private string _text;
    [ObservableProperty] private string _weightInput;
    [ObservableProperty] private string _colorInput;
    [ObservableProperty] private IBrush _colorBrush;
    [ObservableProperty] private bool _hasText;



    // public



    public WidgetOption Option => _option;
    public string Placeholder { get; }
    public IReadOnlyList<ColorChoice> Palette { get; }



    // constructor



    public OptionRowViewModel(WidgetOptionList list, WidgetOption option, string placeholder)
    {
        _list = list;
        _option = option;
        Placeholder = placeholder;
        Palette = WidgetOptionList.Palette.Select(hex => new ColorChoice(hex, PickColor)).ToList();

        _text = "";
        _weightInput = "";
        _colorInput = "";
        _colorBrush = Brushes.Transparent;
        Show();

        _option.Changed += Show;
    }



    // ui events



    // typing goes straight to the list (the empty last row filling in adds an option)
    partial void OnTextChanged(string value) => _list.SetText(_option, value ?? "");



    public void Commit()
    {
        if (!double.TryParse(WeightInput.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var weight)
            || !_list.TrySetWeight(_option, weight)) WeightInput = Format(_option.Weight);
    }



    // the flyout's hex box (a popup, so not committed on leave): applied as soon as it's a whole colour
    partial void OnColorInputChanged(string value)
    {
        if (WidgetOptionList.ParseColor(value ?? "") is { } color && color != _option.Color) _list.TrySetColor(_option, color);
    }



    private void PickColor(string hex) => _list.TrySetColor(_option, hex);



    // drag drop: the handle (only on a filled row)



    public object? GetDragPayload() => _option.IsEmpty ? null : new WidgetOptionDragPayload(_list, _option);



    // model events



    private void Show()
    {
        if (Text != _option.Text) Text = _option.Text;
        WeightInput = Format(_option.Weight);
        ColorInput = _option.Color;
        ColorBrush = Color.TryParse(_option.Color, out var color) ? new SolidColorBrush(color) : Brushes.Transparent;
        HasText = !_option.IsEmpty;
    }



    private static string Format(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);



    // disposal



    public override void Dispose() => _option.Changed -= Show;
}



// one colour in an option's palette flyout
public sealed partial class ColorChoice(string hex, Action<string> pick)
{
    public IBrush Brush { get; } = new SolidColorBrush(Color.Parse(hex));

    [RelayCommand] private void OnPressed() => pick(hex);
}
