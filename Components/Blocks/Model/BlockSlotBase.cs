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

    // set by an owner that sometimes forbids blocks here, returns why (e.g. "this row already runs a binding") or null if allowed
    public Func<string?>? Lock { get; set; }
    public string? LockReason => Lock?.Invoke();

    // set by an owner that only takes some blocks of the accepted type (e.g. a value slot: only kinds that convert)
    public Func<BlockModel, bool>? Filter { get; set; }

    // the block this slot belongs to (set by TrackSlots), so its errors can name it
    public virtual BlockModel? Owner { get; set; }

    // the value kind blocks here should give: Bool for condition slots, the slot's kind for value slots, null otherwise
    // an "any type" block (Convert, Get Parameter) placed here switches to it if it doesn't already fit
    public ValueKind? WantedKind { get; set; }



    // events



    public event Action? Changed; // the slot's contents, or anything nested inside them, changed



    // constructor



    // acceptedType is ExecutableBlock for slots that run blocks, ConditionBlock for slots that check something
    protected BlockSlotBase(string label, Type acceptedType)
    {
        _label = label;
        _acceptedType = acceptedType;
        WantedKind = acceptedType == typeof(ConditionBlock) ? ValueKind.Bool : null;
    }



    // public api



    // the right kind of block (as it is, or once an "any type" block switches kind), and the slot isn't locked right now
    // (drag and drop asks this too)
    public bool Accepts(BlockModel block) => LockReason == null && (Fits(block) || FitsOnceAdapted(block));



    // the block as it is: the accepted type (condition slots take any bool value block) and the owner's filter
    public bool Fits(BlockModel block) =>
        (_acceptedType.IsInstanceOfType(block) || (_acceptedType == typeof(ConditionBlock) && ConditionBlock.IsCondition(block)))
        && (Filter?.Invoke(block) ?? true);



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



    // slots call this just before placing a block Accepts said yes to: an "any type" block switches to the wanted kind
    protected void Adapt(BlockModel block)
    {
        if (block is AdaptableValueBlock adaptable && !Fits(block) && WantedKind is { } kind)
            adaptable.SetKindForPlacement(kind);
    }



    protected void Attach(BlockModel block)
    {
        block.Changed += RaiseChanged;
        block.ParentSlot = this;
    }



    protected void Detach(BlockModel block)
    {
        block.Changed -= RaiseChanged;
        if (block.ParentSlot == this) block.ParentSlot = null;
    }



    // helpers



    // a not-yet-placed "any type" block that would fit once it switches to the wanted kind
    private bool FitsOnceAdapted(BlockModel block) =>
        block is AdaptableValueBlock adaptable && adaptable.ParentSlot == null && WantedKind is { } kind
        && adaptable.CanBe(kind) && adaptable.WithKind(kind, () => Fits(block));



    protected void RaiseChanged() => Changed?.Invoke();



    // disposal



    public abstract void Dispose();
}
