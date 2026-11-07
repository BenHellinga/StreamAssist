using System;
using System.Collections.Generic;
using System.Linq;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.UI;



// the kinds of slot a block can go in, for the lists' type filter: picking one shows only the blocks that kind of slot
// would take (the same check as a drop, against a stand-in slot of that kind). Clicking an empty slot asks the lists
// to show its kind (Requested); a list that doesn't have that kind ignores it
public static class SlotTags
{
    // constants



    public const string All = "All";



    // private



    // in the order the dropdown lists them, each with a stand-in slot of its kind (components add theirs, RegisterKind)
    private static readonly List<(string Tag, BlockSlotBase Slot)> Kinds =
    [
        ("Action", new BlockSlot("", typeof(ExecutableBlock))),
        ("Condition", new BlockSlot("", typeof(ConditionBlock))),
        ("Text", new ValueSlot<string>("").BlockSlot),
        ("Int", new ValueSlot<int>("").BlockSlot),
        ("Double", new ValueSlot<double>("").BlockSlot),
        ("Char", new ValueSlot<char>("").BlockSlot),
        ("Any Value", new AnyValueSlot("").BlockSlot),
        ("Dropdown", new MenuSlot("", () => [], "").BlockSlot),
    ];



    // events



    public static event Action<string>? Requested; // an empty slot was clicked: its tag



    // public api



    // a component's own kind of slot, by the blocks it takes (e.g. the chatbot's chat blocks)
    public static void RegisterKind(string tag, Type acceptedType)
    {
        if (Kinds.Any(k => k.Tag == tag)) return;
        Kinds.Add((tag, new BlockSlot("", acceptedType)));
    }



    // the tags at least one of these blocks fits, in the dropdown's order, All first
    public static List<string> TagsFor(IEnumerable<BlockModel> blocks)
    {
        var list = blocks.ToList();
        return [All, .. Kinds.Where(k => list.Any(b => k.Slot.Accepts(b))).Select(k => k.Tag)];
    }



    public static bool Fits(BlockModel block, string tag) =>
        tag == All || Kinds.FirstOrDefault(k => k.Tag == tag).Slot?.Accepts(block) == true;



    // the tag of a slot (a value slot by its kind; a true/false value slot takes the same blocks as a condition slot)
    public static string? TagOf(BlockSlotBase slot)
    {
        if (slot is BlockSlot { ValueSlot: { } owner }) slot = owner;

        return slot switch
        {
            MenuSlot => "Dropdown",
            ValueSlot { Kind: null } => "Any Value",
            ValueSlot { Kind: ValueKind.Bool } => "Condition",
            ValueSlot { Kind: { } kind } => kind.ToString(),
            _ when typeof(ValueBlock).IsAssignableFrom(slot.AcceptedType) && !typeof(ConditionBlock).IsAssignableFrom(slot.AcceptedType) => "Any Value",
            _ => Kinds.FirstOrDefault(k => k.Slot is BlockSlot { ValueSlot: null } && k.Slot.AcceptedType.IsAssignableFrom(slot.AcceptedType)).Tag,
        };
    }



    // an empty slot was clicked: the lists that have its tag show it
    public static void Request(BlockSlotBase slot)
    {
        if (TagOf(slot) is { } tag) Requested?.Invoke(tag);
    }
}



// a view model whose empty slot can be clicked to filter the lists by its kind (the slot's DropZone or value box asks)
public interface ISlotTagSource
{
    BlockSlotBase TagSlot { get; }
}
