using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace StreamAssist.Styles;



// spacing that's worked out from the base values in Spacing.axaml (the line widths, LineGap, ContentGap, the content
// gaps, ...), so only those are set anywhere and everything else follows them, live (the Themes tab changes them).
// XAML can't build a margin out of resources, so views name a recipe instead: Margin="{s:Gap ScrollInset}"
public static class SpacingRecipes
{
    // the base values (Spacing.axaml), what the Themes tab shows, with the letters recipes use for them
    public static readonly IReadOnlyList<(string Key, string Letter, string Label)> BaseValues =
    [
        ("ThinLineThickness", "TN", "Thin line (dividers)"),
        ("ThickLineThickness", "TK", "Thick line (splitters)"),
        ("LineGap", "L", "Line gap (kept clear around every line)"),
        ("ContentGap", "C", "Content gap (a line to content)"),
        ("SmallHorizontalGap", "SH", "Small gap, side by side"),
        ("MediumHorizontalGap", "MH", "Medium gap, side by side"),
        ("LargeHorizontalGap", "LH", "Large gap, side by side"),
        ("SmallVerticalGap", "SV", "Small gap, stacked"),
        ("MediumVerticalGap", "MV", "Medium gap, stacked (around subheadings)"),
        ("LargeVerticalGap", "LV", "Large gap, stacked (around big titles)"),
        ("CornerGap", "CG", "Corner buttons from their box's edge"),
        ("BoxTextInset", "BT", "Text inside a bind box from its edge"),
    ];



    // name -> recipe: one part = a number (Spacing), several = a margin (left,top,right,bottom like any Thickness).
    // A part is a sum of letters and numbers, e.g. C-L, LV-C-5, -L. P is ContentGap - LineGap (ContentPadding)
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
        ["BigTitleMargin"] = "0,LV-C-5,0,-4", // the 18px font leaves ~5 above its capitals and ~4 under its letters

        // content to content
        ["SmallGapBefore"] = "SH,0,0,0",
        ["SmallGapSides"] = "SH,0,SH,0",

        // inside blocks, cards and tables (to be revised)
        ["BlockTitleMargin"] = "P,P-4,P,0",
        ["BlockBodyMargin"] = "P,SV,P,0",
        ["BlockSlotsMargin"] = "0,SV,0,0",
        ["SlotLabelMargin"] = "P,0,SH,0",
        ["SlotLabelTextMargin"] = "P,4,0,0",
        ["ConnectorMargin"] = "SH,0,0,SV",
        ["CardTitleMargin"] = "P,P,P,2",
        ["CardBodyMargin"] = "P,0,P,P",
        ["SearchRowMargin"] = "P,0,P,C",
        ["TableHeaderLineMargin"] = "0,C,0,C-2.5",
        ["CornerMargin"] = "CG,CG,CG,CG",
        ["SortButtonsMargin"] = "0,0,CG,0",
        ["KeyLabelMargin"] = "CG,0,CG,0",
        ["BoxTextMargin"] = "BT,0,0,0",
        ["BindLabelMargin"] = "BT,0,BT,2",
    };



    // the recipe's value with the base values as they are now (a double, or a Thickness)
    public static object Compute(string name)
    {
        if (!Recipes.TryGetValue(name, out var recipe)) throw new ArgumentException($"no spacing recipe '{name}'");

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
        foreach (Match term in Regex.Matches(part, "([+-]?)([A-Z]+|[0-9.]+)"))
        {
            var value = term.Groups[2].Value switch
            {
                "P" => Base("ContentGap") - Base("LineGap"),
                var letter when char.IsLetter(letter[0]) => Base(BaseValues.First(b => b.Letter == letter).Key),
                var number => double.Parse(number, CultureInfo.InvariantCulture),
            };
            total += term.Groups[1].Value == "-" ? -value : value;
        }

        return total;
    }



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
