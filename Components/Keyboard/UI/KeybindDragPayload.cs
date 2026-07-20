using System;
using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Components.AppAction.UI;



// data class for a keybind drag payload
public sealed class KeybindDragPayload
{
    // private



    private readonly KeybindModel _keybind;
    private readonly Action? _removeFromSource; // action to remove object from source collection



    // public



    public KeybindModel Keybind => _keybind;
    public Action? RemoveFromSource => _removeFromSource; 



    // constructor



    public KeybindDragPayload(KeybindModel keybind, Action? removeFromSource = null)
    {
        _keybind = keybind;
        _removeFromSource = removeFromSource;
    }
}