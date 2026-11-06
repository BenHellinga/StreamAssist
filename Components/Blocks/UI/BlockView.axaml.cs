using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Blocks.UI;



public partial class BlockView : ViewBase
{
    // the card under the pointer: only it shows its buttons, not the blocks it's nested in (they're all "pointer over")
    private static BlockView? _hovered;



    public BlockView()
    {
        InitializeComponent();

        // handled moves too, so the card still hears them over its text boxes and dropdowns (they mark them handled)
        Card.AddHandler(PointerMovedEvent, OnCardPointerMoved, RoutingStrategies.Bubble, handledEventsToo: true);
    }



    // pointer events bubble up through every card the pointer is in, so a card only takes the hover when the
    // pointer's nearest block is itself, and not while it's on a bind box inside it (a Clickable box: it shows its own
    // buttons)
    private void OnCardPointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.Source is not Visual source || source.FindAncestorOfType<BlockView>(includeSelf: true) != this) return;

        var onBindBox = source.GetSelfAndVisualAncestors().TakeWhile(v => v != this)
            .Any(v => v is Border border && border.Classes.Contains("Clickable"));
        var hovered = onBindBox ? null : this;
        if (_hovered != hovered) SetHovered(hovered);
    }



    private void OnCardPointerExited(object? sender, PointerEventArgs e)
    {
        if (_hovered == this) SetHovered(null);
    }



    private static void SetHovered(BlockView? view)
    {
        _hovered?.ShowButtons(false);
        _hovered = view;
        _hovered?.ShowButtons(true);
    }



    private void ShowButtons(bool show)
    {
        Buttons.Opacity = show ? 1 : 0;
        Buttons.IsHitTestVisible = show;
    }



    // clicking a collapsed block expands it
    private void OnCollapsedPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not BlockViewModel vm) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        vm.ExpandPressedCommand.Execute(null);
        e.Handled = true;
    }
}
