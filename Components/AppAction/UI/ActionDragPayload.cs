using System;
using StreamAssist.Components.AppAction.Model;

namespace StreamAssist.Components.AppAction.UI;



public sealed class ActionDragPayload
{
    // private



    private readonly ActionModel _action;
    private readonly Action? _removeFromSource;



    // public



    public ActionModel Action => _action;
    public Action? Callback => _removeFromSource;



    // constructor



    public ActionDragPayload(ActionModel action, Action? removeFromSource = null)
    {
        _action = action;
        _removeFromSource = removeFromSource;
    }
}