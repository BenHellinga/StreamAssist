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
    public static readonly StyledProperty<double> MinTextSizeProperty = AvaloniaProperty.Register<WheelPreview, double>(nameof(MinTextSize), 4);

    public IReadOnlyList<WheelSlice>? Slices { get => GetValue(SlicesProperty); set => SetValue(SlicesProperty, value); }
    public double Rotation { get => GetValue(RotationProperty); set => SetValue(RotationProperty, value); }
    public int Highlight { get => GetValue(HighlightProperty); set => SetValue(HighlightProperty, value); } // the winner showing, -1 none
    public double MinTextSize { get => GetValue(MinTextSizeProperty); set => SetValue(MinTextSizeProperty, value); } // slice text, a % of the radius (the theme's)



    static WheelPreview() => AffectsRender<WheelPreview>(SlicesProperty, RotationProperty, HighlightProperty, MinTextSizeProperty);



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

            // the label along the slice's middle, towards the rim: as big as the slice allows, smaller to fit its length
            // (down to the min), then cut with … (as the page draws it)
            var mid = (slice.Start + slice.End) / 2 + Rotation - 90;
            var span = (slice.End - slice.Start) * Math.PI / 180;
            var min = r * MinTextSize / 100;
            var fontSize = Math.Max(min, Math.Min(r * 0.11, r * span * 0.45));
            var width = Fit(slice.Text, fontSize, double.PositiveInfinity, Brushes.White).Width;
            if (width > r * 0.72) fontSize = Math.Max(min, fontSize * r * 0.72 / width);
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



// a Plinko board's preview: the same drawing as its page (the board fitted in, the buckets tinted with their names and
// counts, the walls, the pins, the balls, a finished drop's result in front), redrawn each time the board moves
public sealed class PlinkoPreview : Control
{
    public static readonly StyledProperty<PlinkoFrame?> FrameProperty = AvaloniaProperty.Register<PlinkoPreview, PlinkoFrame?>(nameof(Frame));

    public PlinkoFrame? Frame { get => GetValue(FrameProperty); set => SetValue(FrameProperty, value); }



    static PlinkoPreview() => AffectsRender<PlinkoPreview>(FrameProperty);



    public override void Render(DrawingContext context)
    {
        if (Frame is not { } frame) return;
        var board = frame.Board;
        var w = Bounds.Width;
        var h = Bounds.Height;
        var scale = Math.Min(w / (board.Right - board.Left), h / (board.Bottom - board.Top));
        if (scale <= 0) return;

        // the board at the left (next to the divider), centred up and down
        Point At(double x, double y) => new((x - board.Left) * scale, h / 2 + (y - (board.Top + board.Bottom) / 2) * scale);

        // the buckets, tinted (their names go on top of everything, below)
        foreach (var bucket in board.Buckets)
        {
            var color = Color.TryParse(bucket.Color, out var c) ? c : Colors.Gray;
            context.FillRectangle(new SolidColorBrush(color, 0.25), new Rect(At(bucket.Left, board.BucketTop), At(bucket.Right, board.Floor)));
        }

        // the walls, floor and dividers
        var wall = new Pen(new SolidColorBrush(Color.FromArgb(217, 255, 255, 255)), Math.Max(1, 0.06 * scale), lineCap: PenLineCap.Round);
        foreach (var s in board.Drawn) context.DrawLine(wall, At(s.X1, s.Y1), At(s.X2, s.Y2));

        // the pins
        var pinFill = new SolidColorBrush(Color.FromRgb(158, 158, 158));
        var pinEdge = new Pen(Brushes.Black, Math.Max(1, 0.03 * scale));
        foreach (var (x, y) in board.Pins()) context.DrawEllipse(pinFill, pinEdge, At(x, y), board.PinRadius * scale, board.PinRadius * scale);

        // the balls
        using (context.PushOpacity(frame.Alpha))
        {
            var ballEdge = new Pen(new SolidColorBrush(Color.FromArgb(153, 0, 0, 0)), Math.Max(1, 0.04 * scale));
            foreach (var ball in frame.Balls)
            {
                var color = Color.TryParse(ball.Color, out var c) ? c : Colors.White;
                context.DrawEllipse(new SolidColorBrush(color), ballEdge, At(ball.X, ball.Y), board.BallRadius * scale, board.BallRadius * scale);
            }
        }

        // each bucket's name over how many have landed in it (0 until one does), in its middle, in front of the balls:
        // both as big as the name fits its width (down to the min, then cut with …)
        var outline = new Pen(new SolidColorBrush(Color.FromArgb(204, 0, 0, 0)), Math.Max(1, scale * 0.05));
        for (var i = 0; i < board.Buckets.Count; i++)
        {
            var bucket = board.Buckets[i];
            var rect = new Rect(At(bucket.Left, board.BucketTop), At(bucket.Right, board.Floor));
            var width = rect.Width * 0.85;
            var size = FitSize(bucket.Text, PlinkoBoard.LabelMax * scale, PlinkoBoard.LabelMin * scale, width);
            var label = Fit(bucket.Text, size, width);
            var count = Fit((i < frame.Counts.Count ? frame.Counts[i] : 0).ToString(CultureInfo.InvariantCulture), size, width);
            DrawOutlined(context, label, new Point(rect.Center.X - label.Width / 2, rect.Center.Y - size * 0.6 - label.Height / 2), outline);
            DrawOutlined(context, count, new Point(rect.Center.X - count.Width / 2, rect.Center.Y + size * 0.6 - count.Height / 2), outline);
        }

        // a finished drop's result, in front: a box like the wheel's winner, its border the bucket's colour
        if (frame.Result is { } result)
        {
            using var _ = context.PushOpacity(result.Alpha);
            var size = 1.1 * scale;
            var text = Fit(result.Text, size, (board.Right - board.Left) * scale * 0.75);
            var pad = size * 0.5;
            var center = At(0, board.ResultY);
            var box = new Rect(center.X - text.Width / 2 - pad, center.Y - size * 0.85, text.Width + pad * 2, size * 1.7);
            var border = Color.TryParse(result.Color, out var bc) ? bc : Colors.White;
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(235, 15, 15, 15)), new Pen(new SolidColorBrush(border), Math.Max(2, size * 0.12)),
                box, box.Height * 0.25, box.Height * 0.25);
            context.DrawText(text, new Point(center.X - text.Width / 2, center.Y - text.Height / 2));
        }
    }



    private static FormattedText Text(string text, double size) => new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
        new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold), size, Brushes.White);



    // the biggest size from max down to min the text fits the width at (Fit cuts it if even the min doesn't)
    private static double FitSize(string text, double max, double min, double width)
    {
        var measured = Text(text, max).Width;
        return measured > width ? Math.Max(min, max * width / measured) : max;
    }



    // the text at that size, cut with … to fit the width
    private static FormattedText Fit(string text, double size, double width)
    {
        var formatted = Text(text, size);
        for (var cut = text; formatted.Width > width && cut.Length > 1;)
        {
            cut = cut[..^1];
            formatted = Text(cut + "…", size);
        }
        return formatted;
    }



    // white text with a dark outline, readable on anything
    private static void DrawOutlined(DrawingContext context, FormattedText text, Point at, Pen outline)
    {
        if (text.BuildGeometry(at) is { } geometry) context.DrawGeometry(Brushes.White, outline, geometry);
    }
}
