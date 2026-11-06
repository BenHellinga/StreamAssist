using StreamAssist.Components.Dashboard.Model;

namespace StreamAssist.Components.Dashboard.UI;



// a dock being dragged by its grip onto a side of another dock (moves it)
public sealed class DockDragPayload(DockModel dock)
{
    public DockModel Dock => dock;
}



// a card from the dock list being dragged onto a side of a dock, or onto the empty dashboard (adds one of that kind)
public sealed class NewDockPayload(DockKind kind)
{
    public DockKind Kind => kind;
}
