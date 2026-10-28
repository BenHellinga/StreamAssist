using Avalonia.Input;

namespace StreamAssist.Core.Dragging;



internal static class DragDropPayload
{
    public static readonly DataFormat<object> Format = DataFormat.CreateInProcessFormat<object>("StreamAssist.Core.DragDropPayload");
}