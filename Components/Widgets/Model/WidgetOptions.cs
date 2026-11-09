using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Core.Utilities;

namespace StreamAssist.Components.Widgets.Model;



// one option of a widget's list (a wheel's slice, a plinko board's bucket): its text, colour (#RRGGBB) and weight (twice
// as heavy is twice as wide). The list is a growing list: the empty last row is where a new one is typed, emptying
// one's text removes it
public sealed class WidgetOption : IDisposable
{
    private string _text = "";
    private string _color = "";
    private double _weight = 1;

    public string Text => _text;
    public string Color => _color;
    public double Weight => _weight;
    public bool IsEmpty => _text == "";

    public event Action? Changed;



    public void SetText(string text)
    {
        if (text == _text) return;
        _text = text;
        Changed?.Invoke();
    }



    public void SetColor(string color)
    {
        if (color == _color) return;
        _color = color;
        Changed?.Invoke();
    }



    public void SetWeight(double weight)
    {
        if (weight == _weight) return;
        _weight = weight;
        Changed?.Invoke();
    }



    public void Dispose() { }
}



// how an option is saved
public sealed class WidgetOptionData
{
    [JsonPropertyName("text")] public string Text { get; set; } = "";
    [JsonPropertyName("color")] public string Color { get; set; } = "";
    [JsonPropertyName("weight")] public double Weight { get; set; } = 1;
}



// a widget's options in order (a wheel's, a plinko board's buckets): added by text (the same text again is left alone),
// removed by text (case ignored), reordered, each with a weight and a colour (new ones take the palette's next). The
// empty last row already has the colour a new one would get, so its swatch shows it
public sealed class WidgetOptionList : IDisposable
{
    // constants



    public const double MaxWeight = 1000;

    // the colours new options get in turn (and a plinko board's balls, at random)
    public static readonly IReadOnlyList<string> Palette =
        ["#E6194B", "#3CB44B", "#FFE119", "#4363D8", "#F58231", "#911EB4", "#46F0F0", "#F032E6", "#BCF60C", "#FABEBE", "#008080", "#9A6324"];



    // private



    private readonly GrowingList<WidgetOption> _options;
    private readonly string _noun; // what an option is called in errors, e.g. "wheel option"
    private readonly string _owner; // what its widget is called in errors, e.g. "wheel"
    private readonly Func<string> _ownerName;



    // public



    public IReadOnlyList<WidgetOption> Items => _options.Items; // the empty last row included
    public IReadOnlyList<WidgetOption> Filled => _options.NonEmptyItems.ToList();



    // events



    public event Action? Changed; // one was added, removed, moved or changed



    // constructor



    public WidgetOptionList(string noun, string owner, Func<string> ownerName)
    {
        _noun = noun;
        _owner = owner;
        _ownerName = ownerName;
        _options = new(() => new WidgetOption(), o => o.IsEmpty, SubscribeToOption, UnsubscribeFromOption);
        _options.Changed += OnOptionsChanged;
        _options.ItemMoved += OnOptionMoved;
        PrepareEmptyRow();
    }



    // public api



    // a new option (the empty row filled in); one with that text already is left as it is. color empty picks the next
    // one from the palette
    public bool TryAdd(string text, double weight = 1, string color = "")
    {
        var trimmed = text.Trim();
        if (trimmed == "")
        {
            AppLog.WriteError($"can't add a {_noun}, no text");
            return false;
        }

        if (!(weight is > 0 and <= MaxWeight))
        {
            AppLog.WriteError($"can't add {_noun} '{trimmed}', weight {weight} isn't above 0 and up to {MaxWeight}");
            return false;
        }

        var parsed = color.Trim() == "" ? NextColor() : ParseColor(color);
        if (parsed == null)
        {
            AppLog.WriteError($"can't add {_noun} '{trimmed}', '{color}' isn't a colour like #FF8800");
            return false;
        }

        if (Find(trimmed) != null) return true;

        var row = _options.Items[^1];
        row.SetColor(parsed);
        row.SetWeight(weight);
        row.SetText(trimmed); // the row is filled now: the growing list adds the next empty one
        return true;
    }



    public bool TryRemove(string text)
    {
        if (Find(text) is not { } option)
        {
            AppLog.WriteError($"can't remove {_noun} '{text.Trim()}', not on {_owner} '{_ownerName()}'");
            return false;
        }

        option.SetText(""); // an empty row goes
        return true;
    }



    public void Clear()
    {
        foreach (var option in Filled) option.SetText("");
    }



    public bool TrySetWeight(WidgetOption option, double weight)
    {
        if (!(weight is > 0 and <= MaxWeight))
        {
            AppLog.WriteError($"can't set a {_noun}'s weight to {weight}, it's above 0 and up to {MaxWeight}");
            return false;
        }

        option.SetWeight(weight);
        return true;
    }



    public bool TrySetColor(WidgetOption option, string color)
    {
        if (ParseColor(color) is not { } parsed)
        {
            AppLog.WriteError($"can't set a {_noun}'s colour, '{color}' isn't a colour like #FF8800");
            return false;
        }

        option.SetColor(parsed);
        return true;
    }



    // typing in a row's box (the empty row's text adds an option, emptying one removes it)
    public void SetText(WidgetOption option, string text) => option.SetText(text.Trim() == "" ? "" : text);



    // dropped on another row: goes before it (the empty last row can't move)
    public bool TryMove(WidgetOption option, int index)
    {
        if (!_options.Move(option, index))
        {
            AppLog.WriteError($"can't move that {_noun} there");
            return false;
        }
        return true;
    }



    public WidgetOption? Find(string text) =>
        _options.NonEmptyItems.FirstOrDefault(o => string.Equals(o.Text.Trim(), text.Trim(), StringComparison.OrdinalIgnoreCase));



    // saving



    public List<WidgetOptionData> GetData() =>
        _options.NonEmptyItems.Select(o => new WidgetOptionData { Text = o.Text, Color = o.Color, Weight = o.Weight }).ToList();



    public void LoadData(List<WidgetOptionData>? data) => _options.Load((data ?? []).Select(item =>
    {
        var option = new WidgetOption();
        option.SetColor(ParseColor(item.Color) ?? Palette[0]);
        option.SetWeight(item.Weight is > 0 and <= MaxWeight ? item.Weight : 1);
        option.SetText(item.Text.Trim());
        return option;
    }));



    // helpers



    // "#RGB" or "#RRGGBB" (the # can be left out) as "#RRGGBB", null if it isn't one
    public static string? ParseColor(string color)
    {
        var hex = color.Trim().TrimStart('#');
        if (hex.Length == 3) hex = string.Concat(hex.Select(c => $"{c}{c}"));
        return hex.Length == 6 && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _) ? "#" + hex.ToUpperInvariant() : null;
    }



    private string NextColor() => Palette[_options.NonEmptyItems.Count() % Palette.Count];



    private void PrepareEmptyRow()
    {
        if (_options.Items.Count > 0 && _options.Items[^1] is { IsEmpty: true } empty && empty.Color == "")
            empty.SetColor(NextColor());
    }



    // model callbacks



    private void OnOptionsChanged()
    {
        PrepareEmptyRow();
        Changed?.Invoke();
    }



    private void OnOptionMoved(WidgetOption _, int from, int to) => Changed?.Invoke();



    private void OnOptionChanged()
    {
        _options.Normalize();
        PrepareEmptyRow();
        Changed?.Invoke();
    }



    private void SubscribeToOption(WidgetOption option) => option.Changed += OnOptionChanged;
    private void UnsubscribeFromOption(WidgetOption option) => option.Changed -= OnOptionChanged;



    // disposal



    public void Dispose()
    {
        _options.Changed -= OnOptionsChanged;
        _options.ItemMoved -= OnOptionMoved;
        _options.Dispose();
    }
}
