using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Blocks.Model;



// which heading a block is listed under in the block list
public enum BlockCategory { Container, Condition, Action }



// base for every block (containers, conditions and actions)
public abstract class BlockModel : ModelBase
{
    // private



    private bool _isDisabled;



    // public



    [JsonIgnore] public abstract string Name { get; }
    [JsonIgnore] public abstract string Description { get; }
    [JsonIgnore] public abstract BlockCategory Category { get; }
    [JsonIgnore] public virtual IReadOnlyList<BlockSlotBase> Slots => [];
    [JsonIgnore] public bool IsDisabled => _isDisabled;



    // json



    [JsonInclude] [JsonPropertyName("isDisabled")]
    private bool JsonIsDisabled
    {
        get => _isDisabled;
        set => _isDisabled = value;
    }



    // events



    public event Action<bool>? IsDisabledChanged;
    public event Action? Triggered;
    public event Action? Changed; // this block, or anything nested inside it, changed



    // constructor



    protected BlockModel()
    {
        _isDisabled = false;
    }



    // public api



    public void SetIsDisabled(bool isDisabled)
    {
        if (isDisabled == _isDisabled) return;

        _isDisabled = isDisabled;
        IsDisabledChanged?.Invoke(_isDisabled);
        RaiseChanged();
    }



    // true if the slot is anywhere inside this block, used to stop a block being dropped into itself
    public bool ContainsSlot(BlockSlotBase slot)
    {
        foreach (var ownSlot in Slots)
            if (ownSlot.ContainsSlot(slot)) return true;

        return false;
    }



    // protected



    protected void SetParameter<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        RaiseChanged();
    }



    // call at the end of a container's constructor so changes inside its slots bubble up
    protected void TrackSlots()
    {
        foreach (var slot in Slots)
            slot.Changed += RaiseChanged;
    }



    protected void RaiseTriggered() => Triggered?.Invoke();



    protected void RaiseChanged() => Changed?.Invoke();



    // disposal



    public override void Dispose()
    {
        foreach (var slot in Slots)
            slot.Dispose();
    }
}
