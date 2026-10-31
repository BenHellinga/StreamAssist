using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Blocks.Model;



// which heading a block is listed under in the block list (Chat blocks only appear in the chatbot's list)
// value blocks are listed by the type they return: Condition (bool), Text, Int, Double, Char, or Any (they can return any)
// the heading a block is listed under in the block list (in this order): the general blocks by what they do, then one
// per integration. Chat is the chatbot tab's own list (Match Text, ...)
public enum BlockGroup { Control, Logic, Math, Text, Parameters, Data, App, Twitch, Obs, Voicemeeter, Spotify, Chat }



// base for every block (containers, conditions and actions)
public abstract class BlockModel : ModelBase
{
    // private



    private bool _isDisabled;
    private bool _isCollapsed;



    // public



    [JsonIgnore] public abstract string Name { get; }
    [JsonIgnore] public abstract string Description { get; }
    // the heading it's listed under: the integration its component folder is (e.g. Components/OBS/... is OBS), or for the
    // general blocks in Components/Blocks what kind of block it is (KindGroup). A block can pick its own (e.g. Play Sound)
    [JsonIgnore] public virtual BlockGroup Group => ComponentGroup(GetType().Namespace) ?? KindGroup;
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
    // the heading of a general block (Container, Action, Condition, a value's type), see Group
    [JsonIgnore] protected abstract BlockGroup KindGroup { get; }



    // e.g. throw Fail("there's no parameter 'viewers'"), the run stops and the root doesn't log it again
    protected BlockFailedException Fail(string reason)
    {
        AppLog.WriteError($"{Name}: {reason}");
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



    // the integration a block's namespace (its component folder) belongs to, null for the general blocks
    private static BlockGroup? ComponentGroup(string? ns) => ns switch
    {
        null => null,
        _ when ns.Contains(".Chatbot.ChatBlocks") => BlockGroup.Chat,
        _ when ns.Contains(".Data.") => BlockGroup.Data,
        _ when ns.Contains(".Chatbot.") => BlockGroup.Twitch,
        _ when ns.Contains(".OBS.") => BlockGroup.Obs,
        _ when ns.Contains(".Spotify.") => BlockGroup.Spotify,
        _ when ns.Contains(".Audio.") => BlockGroup.Voicemeeter,
        _ when ns.Contains(".Keyboard.") || ns.Contains(".OutputConsole.") => BlockGroup.App,
        _ => null,
    };
}
