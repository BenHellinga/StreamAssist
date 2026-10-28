using System;
using System.Collections.Generic;

namespace StreamAssist.Components.Blocks.Model;



// a place inside a block (or bindable) where other blocks go, either a single slot or a list
public abstract class BlockSlotBase : IDisposable
{
    // private



    private readonly string _label;
    private readonly Type _acceptedType;



    // public



    public string Label => _label;
    public Type AcceptedType => _acceptedType;
    public abstract IReadOnlyList<BlockModel> Blocks { get; } // blocks directly in this slot
    public virtual IReadOnlyList<BlockSlotBase> InnerSlots => []; // slots directly in this slot (e.g. an if's branches)



    // events



    public event Action? Changed; // the slot's contents, or anything nested inside them, changed



    // constructor



    // acceptedType is ExecutableBlock for slots that run blocks, ConditionBlock for slots that check something
    protected BlockSlotBase(string label, Type acceptedType)
    {
        _label = label;
        _acceptedType = acceptedType;
    }



    // public api



    public bool Accepts(BlockModel block) => _acceptedType.IsInstanceOfType(block);



    // true if the slot is this one or anywhere inside it
    public bool ContainsSlot(BlockSlotBase slot)
    {
        if (this == slot) return true;

        foreach (var inner in InnerSlots)
            if (inner.ContainsSlot(slot)) return true;

        foreach (var block in Blocks)
            if (block.ContainsSlot(slot)) return true;

        return false;
    }



    public abstract void RemoveBlock(BlockModel block);



    // protected



    protected void Attach(BlockModel block) => block.Changed += RaiseChanged;



    protected void Detach(BlockModel block) => block.Changed -= RaiseChanged;



    protected void RaiseChanged() => Changed?.Invoke();



    // disposal



    public abstract void Dispose();
}
