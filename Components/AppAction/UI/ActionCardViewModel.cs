using StreamAssist.Components.Core.Dragging;
using StreamAssist.Components.AppAction.Model;
using StreamAssist.Components.Core;

namespace StreamAssist.Components.AppAction.UI;



public sealed class ActionCardViewModel : ViewModelBase, IDraggable
{
    // private



    private IAppAction _action;



    // public



    public string Name => _action.Name;
    public string Description => _action.Description;



    // constructor



    public ActionCardViewModel(IAppAction action)
    {
        _action = action;
    }



    // drag drop



    public object GetDragPayload() => _action;



    // disposal



    public override void Dispose() {}
}