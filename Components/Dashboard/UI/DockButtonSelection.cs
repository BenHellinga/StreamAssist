using System;
using StreamAssist.Components.Dashboard.Model;

namespace StreamAssist.Components.Dashboard.UI;



// which dock button's binding is open in the bottom panel editor (at most one, like a selected key or bind box).
// Shared because the rows are made deep inside the docks' regions, where the Dashboard tab can't hand them anything
public static class DockButtonSelection
{
    // private



    private static DockButton? _selected;



    // public



    public static DockButton? Selected => _selected;



    // events



    public static event Action? Changed;



    // public api



    // clicking the open bind box again closes it
    public static void Toggle(DockButton button) => Set(_selected == button ? null : button);



    public static void Set(DockButton? button)
    {
        if (_selected == button) return;

        _selected = button;
        Changed?.Invoke();
    }
}
