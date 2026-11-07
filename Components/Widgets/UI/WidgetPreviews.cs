using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using StreamAssist.Components.Widgets.Model;

namespace StreamAssist.Components.Widgets.UI;



// a Mic Bounce's preview: its image at the bottom left, risen by its level, scaled so the whole way up fits (the page
// does the same at its own size)
public sealed class BouncePreview : Control
{
    public static readonly StyledProperty<IImage?> SourceProperty = AvaloniaProperty.Register<BouncePreview, IImage?>(nameof(Source));
    public static readonly StyledProperty<double> LevelProperty = AvaloniaProperty.Register<BouncePreview, double>(nameof(Level));
    public static readonly StyledProperty<double> BounceHeightProperty = AvaloniaProperty.Register<BouncePreview, double>(nameof(BounceHeight));

    public IImage? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
    public double Level { get => GetValue(LevelProperty); set => SetValue(LevelProperty, value); }
    public double BounceHeight { get => GetValue(BounceHeightProperty); set => SetValue(BounceHeightProperty, value); }



    static BouncePreview() => AffectsRender<BouncePreview>(SourceProperty, LevelProperty, BounceHeightProperty);



    public override void Render(DrawingContext context)
    {
        if (Source is not { } image) return;

        var size = image.Size;
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (size.Width <= 0 || size.Height <= 0 || w <= 0 || h <= 0) return;

        var scale = Math.Min(w / size.Width, h / (size.Height + BounceHeight));
        var drawn = new Size(size.Width * scale, size.Height * scale);
        var rise = Level * BounceHeight * scale;
        var rect = new Rect(0, h - drawn.Height - rise, drawn.Width, drawn.Height);
        context.DrawImage(image, new Rect(size), rect);
    }
}



// a Wheel's preview: its slices (0 degrees at the top, clockwise), turned by its rotation, the pointer at the top, and
// once it has stopped the winner in front of the darkened rest, as the page draws it
public sealed class WheelPreview : Control
{
    public static readonly StyledProperty<IReadOnlyList<WheelSlice>?> SlicesProperty = AvaloniaProperty.Register<WheelPreview, IReadOnlyList<WheelSlice>?>(nameof(Slices));
    public static readonly StyledProperty<double> RotationProperty = AvaloniaProperty.Register<WheelPreview, double>(nameof(Rotation));
    public static readonly StyledProperty<int> HighlightProperty = AvaloniaProperty.Register<WheelPreview, int>(nameof(Highlight), -1);

    public IReadOnlyList<WheelSlice>? Slices { get => GetValue(SlicesProperty); set => SetValue(SlicesProperty, value); }
    public double Rotation { get => GetValue(RotationProperty); set => SetValue(RotationProperty, value); }
    public int Highlight { get => GetValue(HighlightProperty); set => SetValue(HighlightProperty, value); } // the winner showing, -1 none



    static WheelPreview() => AffectsRender<WheelPreview>(SlicesProperty, RotationProperty, HighlightProperty);



    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        var r = size / 2 - Math.Max(6, size * 0.06);
        var center = new Point(size / 2, Bounds.Height / 2 + size * 0.03); // at the left: next to the divider
        if (r <= 0) return;

        var slices = Slices ?? [];
        var white = new Pen(new SolidColorBrush(Color.FromArgb(217, 255, 255, 255)), Math.Max(1, size * 0.004));

        if (slices.Count == 0) context.DrawEllipse(new SolidColorBrush(Color.FromArgb(204, 40, 40, 40)), null, center, r, r);

        for (var i = 0; i < slices.Count; i++)
        {
            var slice = slices[i];
            var color = Color.TryParse(slice.Color, out var c) ? c : Colors.Gray;
            context.DrawGeometry(new SolidColorBrush(color), white, Wedge(center, r, slice.Start + Rotation, slice.End + Rotation));

            // the label along the slice's middle, towards the rim
            var mid = (slice.Start + slice.End) / 2 + Rotation - 90;
            var span = (slice.End - slice.Start) * Math.PI / 180;
            var fontSize = Math.Max(6, Math.Min(r * 0.11, r * span * 0.45));
            var text = Fit(slice.Text, fontSize, r * 0.72, Contrast(color));
            using (context.PushTransform(Matrix.CreateTranslation(-text.Width + r * 0.92, -text.Height / 2)
                       * Matrix.CreateRotation(mid * Math.PI / 180) * Matrix.CreateTranslation(center.X, center.Y)))
                context.DrawText(text, new Point(0, 0));
        }

        // the winner: everything else darkens
        if (Highlight >= 0 && Highlight < slices.Count)
            for (var i = 0; i < slices.Count; i++)
                if (i != Highlight)
                    context.DrawGeometry(new SolidColorBrush(Color.FromArgb(166, 0, 0, 0)), null, Wedge(center, r + 1, slices[i].Start + Rotation, slices[i].End + Rotation));

        // the hub and the pointer
        context.DrawEllipse(Brushes.White, new Pen(new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)), Math.Max(1, size * 0.006)), center, r * 0.08, r * 0.08);
        var pw = Math.Max(8, size * 0.05);
        var ph = Math.Max(10, size * 0.07);
        var pointer = new StreamGeometry();
        using (var g = pointer.Open())
        {
            g.BeginFigure(new Point(center.X - pw / 2, center.Y - r - ph * 0.55), true);
            g.LineTo(new Point(center.X + pw / 2, center.Y - r - ph * 0.55));
            g.LineTo(new Point(center.X, center.Y - r + ph * 0.45));
            g.EndFigure(true);
        }
        context.DrawGeometry(Brushes.White, new Pen(new SolidColorBrush(Color.FromArgb(153, 0, 0, 0)), Math.Max(1, size * 0.005)), pointer);

        // the winner's name in front
        if (Highlight >= 0 && Highlight < slices.Count)
        {
            var winner = slices[Highlight];
            var fontSize = r * 0.16;
            var text = Fit(winner.Text, fontSize, r * 1.6, Brushes.White);
            var pad = fontSize * 0.5;
            var box = new Rect(center.X - text.Width / 2 - pad, center.Y - fontSize * 0.85, text.Width + pad * 2, fontSize * 1.7);
            var border = Color.TryParse(winner.Color, out var wc) ? wc : Colors.White;
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(235, 15, 15, 15)), new Pen(new SolidColorBrush(border), Math.Max(2, fontSize * 0.12)),
                box, box.Height * 0.25, box.Height * 0.25);
            context.DrawText(text, new Point(center.X - text.Width / 2, center.Y - text.Height / 2));
        }
    }



    // a slice's wedge from start to end degrees (0 at the top, clockwise)
    private static StreamGeometry Wedge(Point center, double r, double start, double end)
    {
        Point At(double degrees) => new(center.X + r * Math.Sin(degrees * Math.PI / 180), center.Y - r * Math.Cos(degrees * Math.PI / 180));

        var geometry = new StreamGeometry();
        using var g = geometry.Open();
        g.BeginFigure(center, true);
        if (end - start >= 359.99)
        {
            // a whole circle in two halves (one arc can't start and end at the same point)
            g.LineTo(At(start));
            g.ArcTo(At(start + 180), new Size(r, r), 0, false, SweepDirection.Clockwise);
            g.ArcTo(At(start + 360), new Size(r, r), 0, false, SweepDirection.Clockwise);
        }
        else
        {
            g.LineTo(At(start));
            g.ArcTo(At(end), new Size(r, r), 0, end - start > 180, SweepDirection.Clockwise);
        }
        g.EndFigure(true);
        return geometry;
    }



    // the text at that size, cut with … to fit the width
    private static FormattedText Fit(string text, double fontSize, double width, IBrush brush)
    {
        FormattedText Make(string t) => new(t, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold), fontSize, brush);

        var formatted = Make(text);
        for (var cut = text; formatted.Width > width && cut.Length > 1;)
        {
            cut = cut[..^1];
            formatted = Make(cut + "…");
        }
        return formatted;
    }



    // black or white, whichever reads better on the colour
    private static IBrush Contrast(Color color) =>
        0.299 * color.R + 0.587 * color.G + 0.114 * color.B > 150 ? new SolidColorBrush(Color.FromRgb(17, 17, 17)) : Brushes.White;
}
