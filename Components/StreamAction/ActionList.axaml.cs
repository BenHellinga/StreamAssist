using Avalonia.Controls;
using Avalonia.Input;

namespace StreamAssist.Components.StreamAction;



public partial class ActionList : UserControl
{
    public static readonly DataFormat<IStreamAction> ActionDragFormat =
        DataFormat.CreateInProcessFormat<IStreamAction>("StreamAssist.FeatureAction");



    public ActionList()
    {
        InitializeComponent();
    }



    private async void OnActionPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { Tag: IStreamAction action } border) return;
        if (!e.GetCurrentPoint(border).Properties.IsLeftButtonPressed) return;

        var item = new DataTransferItem();
        item.Set(ActionDragFormat, action);

        var data = new DataTransfer();
        data.Add(item);

        await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Copy);
    }
}
