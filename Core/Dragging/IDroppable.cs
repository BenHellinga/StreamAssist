namespace StreamAssist.Core.Dragging;



public interface IDroppable
{
    bool CanAcceptDropPayload(object payload);
    void OnDroppedOn(object payload, object? zoneContext);

    // true: this target lets the drop target around it handle this payload instead (e.g. a list reordering its rows
    // when one of them is dropped on another's box)
    bool PassesOn(object payload) => false;
}