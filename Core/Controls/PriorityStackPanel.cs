using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;

namespace StreamAssist.Core.Controls;



// a horizontal row (like a StackPanel) that, when it doesn't fit the width it's given, leaves out its least important
// children until it does: each child's Priority (attached, higher = kept longer, ties: the later one goes first). E.g.
// a small key's corner buttons drop on/off before drag and remove. Left-out children are laid out outside the panel,
// which clips to itself, so they don't show or take clicks (their own IsVisible is untouched, so bindings to it still
// work; the app turns clipping off for most controls, a zero-size child would still draw); a hidden child (IsVisible
// false) takes no room either way.
// Where it's given unlimited width (an Auto column) nothing is ever left out
public class PriorityStackPanel : Panel
{
    // properties



    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<PriorityStackPanel, double>(nameof(Spacing));

    public static readonly AttachedProperty<int> PriorityProperty =
        AvaloniaProperty.RegisterAttached<PriorityStackPanel, Control, int>("Priority");

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    public static int GetPriority(Control control) => control.GetValue(PriorityProperty);
    public static void SetPriority(Control control, int value) => control.SetValue(PriorityProperty, value);



    // private



    private readonly HashSet<Control> _leftOut = []; // from the last measure



    // constructor



    static PriorityStackPanel()
    {
        AffectsMeasure<PriorityStackPanel>(SpacingProperty);
        AffectsParentMeasure<PriorityStackPanel>(PriorityProperty);
    }



    public PriorityStackPanel()
    {
        ClipToBounds = true;
    }



    // layout



    protected override Size MeasureOverride(Size availableSize)
    {
        var shown = Children.Where(c => c.IsVisible).ToList();
        foreach (var child in shown) child.Measure(Size.Infinity);

        var kept = shown.ToList();
        while (kept.Count > 0 && RowWidth(kept) > availableSize.Width + 1) // up to a pixel short still fits (sizes from scaling, e.g. keys, land a fraction under)
            kept.Remove(kept.OrderBy(GetPriority).ThenByDescending(shown.IndexOf).First());

        _leftOut.Clear();
        foreach (var child in shown.Except(kept)) _leftOut.Add(child);

        return new Size(RowWidth(kept), kept.Count == 0 ? 0 : kept.Max(c => c.DesiredSize.Height));
    }



    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;
            if (_leftOut.Contains(child))
            {
                child.Arrange(new Rect(-child.DesiredSize.Width - 1, 0, child.DesiredSize.Width, finalSize.Height)); // outside, clipped
                continue;
            }

            child.Arrange(new Rect(x, 0, child.DesiredSize.Width, finalSize.Height));
            x += child.DesiredSize.Width + Spacing;
        }

        return finalSize;
    }



    // helpers



    private double RowWidth(List<Control> children) =>
        children.Sum(c => c.DesiredSize.Width) + Spacing * Math.Max(0, children.Count - 1);
}
