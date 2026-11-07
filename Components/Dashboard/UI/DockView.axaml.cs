using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using StreamAssist.Components.Dashboard.Model;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Dashboard.UI;



public partial class DockView : UserControl
{
    // something that can't be used right now is drawn at half strength (a button row's ▶)
    public static readonly IValueConverter DimWhenTrue = new FuncValueConverter<bool, double>(dim => dim ? 0.5 : 1);
    public static readonly IValueConverter HideWhenTrue = new FuncValueConverter<bool, double>(hide => hide ? 0 : 1); // keeps its room



    public DockView()
    {
        InitializeComponent();
        CommitOnLeave.Attach(this); // the name is applied when its box is left

        Card.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        Card.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        Card.AddHandler(DragDrop.DropEvent, OnDrop);
    }



    // drop: the side of this dock nearest the pointer is where the dragged dock (or a new one) goes, outlined while it's over it



    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        e.DragEffects = DragDropEffects.None;

        if (DataContext is not DockViewModel || Payload(e) is not { } payload || !DockViewModel.CanAcceptDrop(payload))
        {
            DropMarker.IsVisible = false;
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        ShowMarker(EdgeAt(e.GetPosition(Card)));
    }



    private void OnDragLeave(object? sender, DragEventArgs e) => DropMarker.IsVisible = false;



    private void OnDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        DropMarker.IsVisible = false;

        if (DataContext is DockViewModel vm && Payload(e) is { } payload)
            vm.Drop(payload, EdgeAt(e.GetPosition(Card)));
    }



    // helpers



    private static object? Payload(DragEventArgs e) => e.DataTransfer.TryGetValue(DragDropPayload.Format);



    // the closest side, measured as a share of the size (so a wide dock still splits top/bottom near those edges)
    private DockEdge EdgeAt(Point point)
    {
        var x = Card.Bounds.Width > 0 ? point.X / Card.Bounds.Width : 0.5;
        var y = Card.Bounds.Height > 0 ? point.Y / Card.Bounds.Height : 0.5;

        var edge = DockEdge.Left;
        var nearest = x;
        if (1 - x < nearest) (edge, nearest) = (DockEdge.Right, 1 - x);
        if (y < nearest) (edge, nearest) = (DockEdge.Top, y);
        if (1 - y < nearest) edge = DockEdge.Bottom;
        return edge;
    }



    // outlines the half the dropped dock would take
    private void ShowMarker(DockEdge edge)
    {
        var halfWidth = Card.Bounds.Width / 2;
        var halfHeight = Card.Bounds.Height / 2;

        DropMarker.Margin = edge switch
        {
            DockEdge.Left => new Thickness(0, 0, halfWidth, 0),
            DockEdge.Right => new Thickness(halfWidth, 0, 0, 0),
            DockEdge.Top => new Thickness(0, 0, 0, halfHeight),
            _ => new Thickness(0, halfHeight, 0, 0),
        };
        DropMarker.IsVisible = true;
    }
}
