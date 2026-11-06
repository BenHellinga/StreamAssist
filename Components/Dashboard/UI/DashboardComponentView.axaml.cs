using Avalonia.Controls;
using Avalonia.Input;
using StreamAssist.Core.Dragging;

namespace StreamAssist.Components.Dashboard.UI;



public partial class DashboardComponentView : UserControl
{
    public DashboardComponentView()
    {
        InitializeComponent();

        Regions.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        Regions.AddHandler(DragDrop.DropEvent, OnDrop);
    }



    // only reached with no docks (a dock handles drops on itself): a card adds the first dock



    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DataContext is DashboardComponentViewModel vm && vm.CanAcceptNewDock(Payload(e))
            ? DragDropEffects.Move : DragDropEffects.None;
    }



    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is DashboardComponentViewModel vm) vm.DropNewDock(Payload(e));
    }



    private static object? Payload(DragEventArgs e) => e.DataTransfer.TryGetValue(DragDropPayload.Format);
}
