using System;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.UI;



// data class for a whole binding being dragged between binding slots (key to key, later key to chat command, ...)
// the target binds a copy, then calls removeFromSource
public sealed class BindableDragPayload
{
    // private



    private readonly BindableModel _bindable;
    private readonly Action? _removeFromSource;



    // public



    public BindableModel Bindable => _bindable;
    public Action? RemoveFromSource => _removeFromSource;



    // constructor



    public BindableDragPayload(BindableModel bindable, Action? removeFromSource = null)
    {
        _bindable = bindable;
        _removeFromSource = removeFromSource;
    }
}
