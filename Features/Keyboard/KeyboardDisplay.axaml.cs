using Avalonia.Controls;
using Avalonia.Input;
using StreamAssist.Features.Shell;
using System.Linq;

namespace StreamAssist.Features.Keyboard;



public partial class KeyboardDisplay : UserControl
{
    public KeyboardDisplay()
    {
        Resources["UnitToPixelConverter"] = UnitToPixelConverter.Instance;
        InitializeComponent();
    }



    private void OnKeyDragOver(object? sender, DragEventArgs e)
    {
        // Only show acceptance if the drag actually carries an action.
        e.DragEffects = e.DataTransfer.Formats.Contains(ActionList.ActionDragFormat)
            ? DragDropEffects.Copy
            : DragDropEffects.None;

        if (sender is Border border)
            border.Classes.Set("dragover", e.DragEffects != DragDropEffects.None);
    }



    private void OnKeyDragLeave(object? sender, DragEventArgs e)
    {
        if (sender is Border border)
            border.Classes.Set("dragover", false);
    }



    private void OnKeyDrop(object? sender, DragEventArgs e)
    {
        if (sender is not Border { DataContext: KeyInfo key } border) return;

        border.Classes.Set("dragover", false);

        if (e.DataTransfer.TryGetValue(ActionList.ActionDragFormat) is { } action)
        {
            key.BoundAction = action;
            AppLog.WriteLine($"Bound '{action.Name}' to {key.Label.Replace("\n", " ")}.");
        }
    }



    private void OnKeyPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { DataContext: KeyInfo key } border) return;

        var props = e.GetCurrentPoint(border).Properties;

        // Right click on a key clears its binding (no-op if already unbound).
        if (props.IsRightButtonPressed)
        {
            if (key.BoundAction is { } bound)
            {
                AppLog.WriteLine($"Unbound '{bound.Name}' from {key.Label.Replace("\n", " ")}.");
                key.BoundAction = null;
            }

            e.Handled = true;
            return;
        }

        // Left click selects the key so its details show in the ActionPanel.
        if (props.IsLeftButtonPressed && DataContext is KeyboardDisplayViewModel vm)
            vm.NotifyKeySelected(key);
    }
}