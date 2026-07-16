namespace StreamAssist.Components.Core.Dragging;



public interface IDroppable
{
    bool CanAcceptDropPayload(object payload);
    void OnDroppedOn(object payload, object? zoneContext);
}