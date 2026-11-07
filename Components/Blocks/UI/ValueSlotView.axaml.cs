using Avalonia.Input;
using Avalonia.Interactivity;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Blocks.UI;



public partial class ValueSlotView : ViewBase
{
    public ValueSlotView()
    {
        InitializeComponent();

        // its empty box clicked (to type or pick in it too): the lists show what fits this slot. With a block placed in
        // it nothing here counts (that block's own slots ask for themselves)
        AddHandler(PointerPressedEvent, (_, _) =>
        {
            if (DataContext is ValueSlotViewModel { Block: null } slot) SlotTags.Request(slot.TagSlot);
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
    }



    private void OnDragOver(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDragOver(DataContext, e);



    private void OnDragLeave(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDragLeave(DataContext, e);



    private void OnDrop(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDrop(DataContext, e);
}
