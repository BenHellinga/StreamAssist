using Avalonia.Controls;
using Avalonia.Input;
using StreamAssist.Core;

namespace StreamAssist.Features.Keyboard;



public partial class ActionList : UserControl
{
    // The drag-data format key used when an action card is dragged out of
    // this list. The eventual key drop target should check for this format.
    public static readonly DataFormat<IFeatureAction> ActionDragFormat = DataFormat.CreateInProcessFormat<IFeatureAction>("StreamAssist.FeatureAction");



    public ActionList()
    {
        InitializeComponent();
    }



    private async void OnActionPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { Tag: IFeatureAction action } border) return;
        if (!e.GetCurrentPoint(border).Properties.IsLeftButtonPressed) return;

        var item = new DataTransferItem();
        item.Set(ActionDragFormat, action);

        var data = new DataTransfer();
        data.Add(item);

        await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Copy);
    }
}
