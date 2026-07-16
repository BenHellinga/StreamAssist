using Avalonia.Input;

namespace StreamAssist.Components.Core.Dragging;



internal static class DragDropPayload
{
    public static readonly DataFormat<object> Format = DataFormat.CreateInProcessFormat<object>("StreamAssist.Core.DragDropPayload");
}