using System;
using StreamAssist.Components.Dashboard.Model;

namespace StreamAssist.Components.Dashboard.UI;



// a copied dock on the clipboard (BlockClipboard kind "dock", the dock as it's saved), pasted onto a blank dock, which
// it takes the place of (DashboardConfigModel.TryPasteDock)
public sealed class DockPastePayload(DockModel dock) : IDisposable
{
    public const string Kind = "dock";

    public DockModel Dock { get; } = dock;

    public void Dispose() => Dock.Dispose();
}
