using StreamAssist.Components.Widgets.Model;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Widgets.UI;



// one kind of widget in the widget list, dragged into the widgets to add one
public sealed class WidgetCardViewModel(WidgetKind kind) : ViewModelBase, IDraggable
{
    public string Name => WidgetModel.KindTitle(kind);

    public string Description => kind switch
    {
        WidgetKind.MicBounce => "An image that rises as you talk, higher the louder you are, with an idle and a talking image",
        WidgetKind.Plinko => "A triangle of pins that balls drop through into buckets at the bottom, as wide as their weights, with pin and land sounds",
        _ => "A wheel of options with colours and weights that spins and lands on a winner, with tick and win sounds",
    };



    public object? GetDragPayload() => new NewWidgetPayload(kind);



    public override void Dispose() { }
}
