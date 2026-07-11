using Avalonia.Controls;
using Avalonia.Input;
using StreamAssist.Components.Main;
using StreamAssist.Components.StreamAction;
using System.Linq;

namespace StreamAssist.Components.Keyboard.UI;



public partial class KeyboardSlot : UserControl
{
    public KeyboardSlot()
    {
        // Resources["UnitToPixelConverter"] = UnitToPixelConverter.Instance;
        InitializeComponent();
    }



    private void OnKeyDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Formats.Contains(ActionList.ActionDragFormat)
            ? DragDropEffects.Copy : DragDropEffects.None;

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
        if (sender is not Border { DataContext: DisplayKey key } border) return;

        border.Classes.Set("dragover", false);

        if (DataContext is not KeyboardSlotViewModel vm || vm.SlotModel.SelectedConfig is null)
        {
            AppLog.WriteError("Could not assign keybind - no config selected");
            return;
        }

        if (e.DataTransfer.TryGetValue(ActionList.ActionDragFormat) is { } action)
            key.BoundAction = action;
    }



    private void OnKeyPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { DataContext: DisplayKey key } border) return;

        var props = e.GetCurrentPoint(border).Properties;

        if (props.IsRightButtonPressed)
        {
            if (key.BoundAction is { } bound)
                key.BoundAction = null;

            e.Handled = true;
            return;
        }

        if (props.IsLeftButtonPressed && DataContext is KeyboardSlotViewModel vm)
            vm.OnKeySelected(key);
    }
}