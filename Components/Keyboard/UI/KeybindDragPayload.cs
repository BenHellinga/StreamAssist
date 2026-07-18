using System;
using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Components.AppAction.UI;



public sealed class KeybindDragPayload
{
    // private



    private readonly KeybindModel _keybind;
    private readonly Func<bool> _removeFromSource;



    // public



    public KeybindModel keybind => _keybind;



    // constructor



    public KeybindDragPayload(KeybindModel keybind, Func<bool>? removeFromSource = null)
    {
        _keybind = keybind;
        _removeFromSource = removeFromSource ?? (() => true);
    }



    // public api



    public bool RemoveFromSource() => _removeFromSource();
}