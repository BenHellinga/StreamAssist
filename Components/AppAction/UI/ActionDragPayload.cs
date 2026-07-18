using System;
using StreamAssist.Components.AppAction.Model;

namespace StreamAssist.Components.AppAction.UI;



public sealed class ActionDragPayload
{
    // private



    private readonly ActionModel _action;
    private readonly Func<bool> _removeFromSource;



    // public



    public ActionModel Action => _action;



    // constructor



    public ActionDragPayload(ActionModel action, Func<bool>? removeFromSource = null)
    {
        _action = action;
        _removeFromSource = removeFromSource ?? (() => true);
    }



    // public api



    public bool TryRemoveFromSource() => _removeFromSource();
}