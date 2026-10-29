using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Blocks.Model;



// which heading a block is listed under in the block list (Chat blocks only appear in the chatbot's list)
// value blocks are listed by the type they return: Condition (bool), Text, Int, Double, Char, or Any (they can return any)
public enum BlockCategory { Container, Condition, Action, Chat, Text, Int, Double, Char, Any }



// base for every block (containers, conditions and actions)
public abstract class BlockModel : ModelBase
{
    // private



    private bool _isDisabled;
    private bool _isCollapsed;



    // public



    [JsonIgnore] public abstract string Name { get; }
    [JsonIgnore] public abstract string Description { get; }
    [JsonIgnore] public abstract BlockCategory Category { get; }
    [JsonIgnore] public virtual IReadOnlyList<BlockSlotBase> Slots => [];
    [JsonIgnore] public virtual bool SlotsBesideSettings => false; // the editor shows the slots to the right of the settings, not under them
    [JsonIgnore] public bool IsDisabled => _isDisabled;
    [JsonIgnore] public bool IsCollapsed => _isCollapsed; // shown as a small box in the editor
    [JsonIgnore] public BlockSlotBase? ParentSlot { get; internal set; } // the slot it's placed in (set by the slot)



    // json



    [JsonInclude] [JsonPropertyName("isDisabled")]
    private bool JsonIsDisabled
    {
        get => _isDisabled;
        set => _isDisabled = value;
    }

    [JsonInclude] [JsonPropertyName("isCollapsed")]
    private bool JsonIsCollapsed
    {
        get => _isCollapsed;
        set => _isCollapsed = value;
    }



    // events



    public event Action<bool>? IsDisabledChanged;
    public event Action<bool>? IsCollapsedChanged;
    public event Action? Triggered;
    public event Action? Changed; // this block, or anything nested inside it, changed



    // constructor



    protected BlockModel()
    {
        _isDisabled = false;
        _isCollapsed = false;
    }



    // public api



    public void SetIsDisabled(bool isDisabled)
    {
        if (isDisabled == _isDisabled) return;

        _isDisabled = isDisabled;
        IsDisabledChanged?.Invoke(_isDisabled);
        RaiseChanged();
    }



    public void SetIsCollapsed(bool isCollapsed)
    {
        if (isCollapsed == _isCollapsed) return;

        _isCollapsed = isCollapsed;
        IsCollapsedChanged?.Invoke(_isCollapsed);
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
        {
            slot.Owner = this;
            slot.Changed += RaiseChanged;
        }
    }



    // logs why this block can't go on (here, where the most is known about it) and returns the exception to throw,
    // e.g. throw Fail("there's no parameter 'viewers'"), the run stops and the root doesn't log it again
    protected BlockFailedException Fail(string reason)
    {
        AppLog.WriteError($"Could not run '{Name}' - {reason}");
        return new BlockFailedException(reason);
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
