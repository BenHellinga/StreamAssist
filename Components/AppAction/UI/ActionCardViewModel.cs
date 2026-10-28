using StreamAssist.Core.Dragging;
using StreamAssist.Components.AppAction.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.AppAction.UI;



public sealed class ActionCardViewModel : ViewModelBase, IDraggable
{
    // private



    private ActionModel _action;



    // public



    public string Name => _action.Name;
    public string Description => _action.Description;



    // constructor



    public ActionCardViewModel(ActionModel action)
    {
        _action = action;
    }



    // drag drop



    public object? GetDragPayload() => new ActionDragPayload(_action);



    // disposal



    public override void Dispose() {}
}