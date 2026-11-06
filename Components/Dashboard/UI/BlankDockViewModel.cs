using System.Linq;
using System.Text.Json;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Components.Dashboard.Model;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Dashboard.UI;



// a blank dock's body: an empty spot that takes a pasted dock (its paste button shows while there's a copied dock on
// the clipboard), which then takes the blank's place
public sealed class BlankDockViewModel(DashboardConfigModel config, DockModel blank) : ViewModelBase, IDroppable
{
    // a copied dock pastes as a DockPastePayload
    static BlankDockViewModel()
    {
        BlockClipboard.RegisterKind(DockPastePayload.Kind, data =>
            data.Deserialize<DockModel>() is { } dock ? new DockPastePayload(dock) : null);
    }



    public bool CanAcceptDropPayload(object payload) =>
        payload is DockPastePayload pasted && pasted.Dock.Kind != DockKind.Blank && config.Docks.Contains(blank);



    public void OnDroppedOn(object payload, object? zoneContext)
    {
        if (payload is DockPastePayload pasted) config.TryPasteDock(blank, pasted.Dock);
    }



    public override void Dispose() { }
}
