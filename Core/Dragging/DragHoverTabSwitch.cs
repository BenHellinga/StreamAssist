using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace StreamAssist.Core.Dragging;



public static class DragHoverTabSwitch
{
    public static readonly AttachedProperty<bool> EnableProperty =
        AvaloniaProperty.RegisterAttached<ListBoxItem, bool>("Enable", typeof(DragHoverTabSwitch));



    public static void SetEnable(ListBoxItem element, bool value) => element.SetValue(EnableProperty, value);
    public static bool GetEnable(ListBoxItem element) => element.GetValue(EnableProperty);



    static DragHoverTabSwitch()
    {
        EnableProperty.Changed.AddClassHandler<ListBoxItem>((item, args) =>
        {
            if (args.NewValue is true)
                item.AddHandler(DragDrop.DragOverEvent, OnDragOver, handledEventsToo: true);
        });
    }



    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        if (sender is not ListBoxItem item) return;
        if (item.Parent is not ListBox listBox) return;

        if (!ReferenceEquals(listBox.SelectedItem, item.DataContext))
            listBox.SelectedItem = item.DataContext;
    }
}