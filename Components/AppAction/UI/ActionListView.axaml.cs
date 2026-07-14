using Avalonia.Controls;
using Avalonia.Input;
using StreamAssist.Components.AppAction.Model;

namespace StreamAssist.Components.AppAction.UI;



public partial class ActionListView : UserControl
{
    public static readonly DataFormat<IAppAction> ActionDragFormat =
        DataFormat.CreateInProcessFormat<IAppAction>("StreamAssist.FeatureAction");



    public ActionListView()
    {
        InitializeComponent();
    }



    private async void OnActionPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { Tag: IAppAction action } border) return;
        if (!e.GetCurrentPoint(border).Properties.IsLeftButtonPressed) return;

        var item = new DataTransferItem();
        item.Set(ActionDragFormat, action);

        var data = new DataTransfer();
        data.Add(item);

        await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Copy);
    }
}
