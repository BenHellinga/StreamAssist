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
    public abstract IReadOnlyList<BlockModel> Blocks { get; }



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



    public abstract void RemoveBlock(BlockModel block);



    // protected



    protected void Attach(BlockModel block) => block.Changed += RaiseChanged;



    protected void Detach(BlockModel block) => block.Changed -= RaiseChanged;



    protected void RaiseChanged() => Changed?.Invoke();



    // disposal



    public abstract void Dispose();
}
