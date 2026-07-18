using System;
using StreamAssist.Components.AppAction.Model;

namespace StreamAssist.Components.AppAction.UI;



public sealed class ActionDragPayload
{
    // private



    private readonly IAppAction _action;
    private readonly Func<bool> _removeFromSource;



    // public



    public IAppAction Action => _action;



    // constructor



    public ActionDragPayload(IAppAction action, Func<bool>? removeFromSource = null)
    {
        _action = action;
        _removeFromSource = removeFromSource ?? (() => true);
    }



    // public api



    public bool RemoveFromSource() => _removeFromSource();
}