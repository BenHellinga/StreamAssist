using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace StreamAssist.Styles;



// a number the Themes tab can change: its resource key, the letter recipes use for it, what the tab calls it, the range
// it accepts, other resources that always take the same number (e.g. the theme's checkbox height), and, for a component's
// value, the subsection it's listed under
public sealed record ThemeValue(string Key, string Letter, string Label, double Min, double Max, string[]? AlsoSets = null, string? Group = null)
{
    public bool Accepts(double value) => value >= Min && value <= Max; // NaN isn't
}



// spacing that's worked out from the base values (Spacing.axaml, and the style values in Sizes.axaml /
// Typography.axaml), so only those are set anywhere and everything else follows them, live (the Themes tab changes
// them). XAML can't build a margin out of resources, so views name a recipe instead: Margin="{s:Gap ScrollInset}"
public static class SpacingRecipes
{
    // the spacing base values (Spacing.axaml), the Themes tab's Spacing section
    public static readonly IReadOnlyList<ThemeValue> BaseValues =
    [
        new("LineGap", "L", "Line gap", 0, 100),
        new("ContentGap", "C", "Content gap", 0, 100),
        new("SmallHorizontalGap", "SH", "Small horizontal gap", 0, 100),
        new("MediumHorizontalGap", "MH", "Medium horizontal gap", 0, 100),
        new("LargeHorizontalGap", "LH", "Large horizontal gap", 0, 100),
        new("SmallVerticalGap", "SV", "Small vertical gap", 0, 100),
        new("MediumVerticalGap", "MV", "Medium vertical gap", 0, 100),
        new("LargeVerticalGap", "LV", "Large vertical gap", 0, 100),
        new("CornerGap", "CG", "Corner gap", 0, 100),
        new("BoxTextInset", "BT", "Box text inset", 0, 100),
    ];



    // the style values (Typography.axaml, Sizes.axaml), the Themes tab's Styles section
    public static readonly IReadOnlyList<ThemeValue> StyleValues =
    [
        new("FontSizeTitle", "FL", "Large text", 6, 72),
        new("FontSizeNormal", "FM", "Medium text", 6, 72),
        new("FontSizeSmall", "FS", "Small text", 6, 72),
        new("ControlHeight", "H", "Control height", 10, 100, ["CheckBoxMinHeight"]),
        new("ControlMinWidth", "WN", "Control min width", 20, 1000),
        new("ControlMaxWidth", "WX", "Control max width", 20, 2000),
        new("SmallButtonSize", "SB", "Corner button size", 5, 100),
        new("IconSize", "I", "Icon size", 2, 100),
        new("BoxBorderWidth", "BW", "Border width", 0, 10),
        new("BoxCornerRadius", "R", "Corner rounding", 0, 50),
        new("ThinLineThickness", "TN", "Thin line", 0, 20),
        new("ThickLineThickness", "TK", "Thick line", 1, 50),
    ];



    // a component's own sizes (Sizes.axaml), the Themes tab's Components section, a subsection per Group
    public static readonly IReadOnlyList<ThemeValue> ComponentValues =
    [
        new("InputBoxMinWidth", "KW", "Box min width", 100, 5000, Group: "Input"),
        new("WidgetPreviewMinWidth", "WPW", "Preview min width", 50, 2000, Group: "Widgets"),
        new("WidgetPreviewMinHeight", "WPH", "Preview min height", 50, 2000, Group: "Widgets"),
        new("WidgetListMaxHeight", "WLH", "Options max height", 50, 5000, Group: "Widgets"),
    ];



    public static IEnumerable<ThemeValue> AllValues => BaseValues.Concat(StyleValues).Concat(ComponentValues);



    // name -> recipe: one part = a number (Spacing), several = a margin (left,top,right,bottom like any Thickness),
    // "radius:" first = a CornerRadius. A part is a sum of terms, each a letter or number or a product of them, e.g.
    // C-L, LV-C-5, -L, WN*1.5. P is ContentGap - LineGap (ContentPadding)
    public static readonly IReadOnlyDictionary<string, string> Recipes = new Dictionary<string, string>
    {
        // lines: the zone each keeps clear (their styles)
        ["LineZoneAll"] = "L,L,L,L", // a box's ring inside its border, the window's edge
        ["HorizontalLineZone"] = "0,L,0,L", // a divider, a row splitter
        ["VerticalLineZone"] = "L,0,L,0", // a vertical divider, a column splitter
        ["VerticalScrollBarMargin"] = "L-12,0,0,0", // the thin bar is a 2px line 2px in from the outer edge of its 16px box
        ["HorizontalScrollBarMargin"] = "0,L-12,0,0",
        ["RingCornerMargin"] = "0,-L,-L,0", // corner buttons out of a box's ring

        // regions: content ContentGap from every line, i.e. P inside the lines' zones
        ["ContentPadding"] = "P",
        ["PadAll"] = "P,P,P,P",
        ["PadSides"] = "P,0,P,0",
        ["PadLeft"] = "P,0,0,0",
        ["PadRight"] = "0,0,P,0",
        ["PadBelow"] = "0,0,0,P",
        ["TopInset"] = "L,L,L,0", // a list or panel right under a line (what follows brings its own gap)
        ["ScrollEdgeMargin"] = "P,P-3,P,P", // a scroll area's visible part (3 of the top P is on its content, for a first item's insert line)
        ["ScrollInset"] = "0,3,0,0",
        ["LinedScrollEdgeMargin"] = "0,P-3,0,P", // a page of sections and dividers: no sides, the sections pad themselves
        // big titles (TextBlock.Title's style, never on a view): the 18px font leaves ~5 above its capitals and ~4 under
        // its letters, taken back so the title's box is its letters and its container's LV is the gap on both sides.
        // Right under a line content is only C away, so a title there (UnderLine) adds LV-C
        ["TitleTrim"] = "0,-5,0,-4",
        ["TitleUnderLineMargin"] = "0,LV-C-5,0,-4",

        // content to content
        ["SmallGapBefore"] = "SH,0,0,0",
        ["SmallGapSides"] = "SH,0,SH,0",

        // inside blocks, cards and tables (to be revised)
        // a block: its title's letters P from the ring (the 18px font leaves ~4 above its capitals and ~4 under its
        // letters), then MV to its body; the body's rows are SV apart, text rows (settings, value boxes) pad their sides
        // P and end P above the ring, nested blocks sit at the ring
        ["BlockTitleMargin"] = "P,P-4,P,P-4", // a block with no settings or slots
        ["BlockTitleBodyMargin"] = "P,P-4,P,-4", // a block with a body under its title
        ["BlockBodyTop"] = "0,MV,0,0",
        ["BlockSlotsMargin"] = "0,SV,0,0", // slots under settings
        ["BlockSlotsPadBelowMargin"] = "0,SV,0,P", // slots under settings
        // a value box sits in an invisible BW border (lit while a block is dragged over it), pulled back so the box
        // itself keeps the gaps: its sides P from the ring, or after a label (or a name) none on that side
        ["ValueDropMargin"] = "P-BW,-BW,P-BW,-BW",
        ["ValueDropAfterLabelMargin"] = "-BW,-BW,P-BW,-BW",
        ["ValueDropBeforeValueMargin"] = "P-BW,-BW,-BW,-BW",
        // a value box's paste button: at its right, CornerGap inside its border, the box's text ending SH before it
        // (the left and top/bottom as a text box's own padding)
        ["PasteInBoxMargin"] = "0,BW+CG,BW+CG,0",
        ["PasteBoxPadding"] = "8,1,CG+SB+SH,1",
        // a blank dock's paste button: in its corner buttons' row (out of the ring like them), left of drag and remove
        ["BlankPasteMargin"] = "0,CG-L,CG+SB+CG+SB+CG-L,0",
        // a row under a box's picture (a microphone box's meter and settings): its sides and below like the picture's
        ["BoxBottomRowMargin"] = "P,0,P,P",
        // a label ending in a colon: the monospace font draws the colon in the middle of its ~0.59em cell, leaving ~0.22em
        // empty after it, taken back so the colon itself is the gap from what follows
        ["ColonTrim"] = "0,0,-FM*0.22,0",
        ["SlotLabelMargin"] = "P,0,SH-FM*0.22,0", // a value slot's label: P from the ring, SH from its box (colon trimmed)
        ["SlotLabelTextMargin"] = "P,4,SH-FM*0.22,0", // a block slot's label (When Done:): SH from its colon to its slot when it's wider than SlotLabelWidth
        ["ConnectorMargin"] = "SH,0,0,SV",
        ["CardTitleMargin"] = "P,P,P,2",
        ["CardBodyMargin"] = "P,0,P,P",
        ["SearchRowMargin"] = "P,0,P,P",
        ["SubgroupIndent"] = "MH,0,0,0", // a block list's subgroup (its heading and cards) under its group's heading
        ["SearchBoxWidth"] = "2*WN", // a list's search box: two minimum boxes wide (its type dropdown is one)
        ["TableHeaderLineMargin"] = "0,C,0,C", // the line under a table's column headers: C from them and from the first row
        ["SmallGapBelow"] = "0,0,0,SV", // a table row: SV to the next (or to the empty row after the last)
        ["SortNamePadding"] = "BT,0,CG+SB+SH+SB+SH,0", // a column name: room on the right for its two corner buttons
        ["CornerMargin"] = "CG,CG,CG,CG",
        ["SortButtonsMargin"] = "0,0,CG,0",
        ["KeyLabelMargin"] = "CG,0,CG,0",
        ["BoxTextMargin"] = "BT,0,0,0",
        ["BindLabelMargin"] = "BT,0,BT,2",


        // borders
        ["BorderAll"] = "BW,BW,BW,BW", // a box's, button's, text box's or dropdown's border
        ["Corners"] = "radius:R", // their rounding
    };



    // the recipe's value with the base values as they are now (a double, a Thickness or a CornerRadius)
    public static object Compute(string name)
    {
        if (!Recipes.TryGetValue(name, out var recipe)) throw new ArgumentException($"no spacing recipe '{name}'");
        if (recipe.StartsWith("radius:")) return new CornerRadius(Evaluate(recipe["radius:".Length..]));

        var parts = recipe.Split(',').Select(Evaluate).ToArray();
        return parts.Length switch
        {
            1 => parts[0],
            2 => new Thickness(parts[0], parts[1]),
            _ => new Thickness(parts[0], parts[1], parts[2], parts[3]),
        };
    }



    private static double Evaluate(string part)
    {
        var total = 0.0;
        foreach (Match term in Regex.Matches(part, @"([+-]?)((?:[A-Z]+|[0-9.]+)(?:\*(?:[A-Z]+|[0-9.]+))*)"))
        {
            var value = term.Groups[2].Value.Split('*').Aggregate(1.0, (product, factor) => product * Factor(factor));
            total += term.Groups[1].Value == "-" ? -value : value;
        }

        return total;
    }



    private static double Factor(string factor) => factor switch
    {
        "P" => Base("ContentGap") - Base("LineGap"),
        _ when char.IsLetter(factor[0]) => Base(AllValues.First(b => b.Letter == factor).Key),
        _ => double.Parse(factor, CultureInfo.InvariantCulture),
    };



    private static double Base(string key) => Application.Current?.TryFindResource(key, out var value) == true && value is double d ? d : 0;
}



// {s:Gap Name}: a spacing recipe's value, worked out again whenever the app's resources change (e.g. a base value edited
// in the Themes tab), so it follows them live
public sealed class GapExtension(string name) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => new RecipeObservable(name).ToBinding();



    private sealed class RecipeObservable(string name) : IObservable<object>
    {
        public IDisposable Subscribe(IObserver<object> observer)
        {
            void OnResourcesChanged(object? sender, ResourcesChangedEventArgs e) => observer.OnNext(SpacingRecipes.Compute(name));

            observer.OnNext(SpacingRecipes.Compute(name));
            var app = Application.Current;
            if (app != null) app.ResourcesChanged += OnResourcesChanged;

            return new Unsubscriber(() =>
            {
                if (app != null) app.ResourcesChanged -= OnResourcesChanged;
            });
        }
    }



    private sealed class Unsubscriber(Action unsubscribe) : IDisposable
    {
        public void Dispose() => unsubscribe();
    }
}
