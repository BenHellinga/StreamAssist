using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Values;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Containers;



public class ForEachBlock : ContainerBlock
{
    // info



    [JsonIgnore] public override string Name => "For Each";
    [JsonIgnore] public override string Description => "Runs the block inside once for each part of a list, e.g. what Get OBS Sources gives. Separator is what's between the parts (\", \" by default, empty = between words); parts are trimmed and empty ones skipped. Each time, {item} (or the name in Item As) is that part.";



    // private



    private string _itemName;
    private readonly ValueSlot<string> _list;
    private readonly ValueSlot<string> _separator;
    private readonly BlockSlot _child;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Item As", ConfiguratorType.Text)] [JsonPropertyName("itemName")]
    public string ItemName
    {
        get => _itemName;
        set => SetParameter(ref _itemName, value);
    }

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("list")]
    private ValueSlotData JsonList
    {
        get => _list.GetData();
        set => _list.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("separator")]
    private ValueSlotData JsonSeparator
    {
        get => _separator.GetData();
        set => _separator.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("child")]
    private PendingBlockData? JsonChild
    {
        get => _child.GetData();
        set => _child.LoadData(value);
    }



    // constructor



    public ForEachBlock()
    {
        _itemName = "item";
        _list = new("List");
        _separator = new("Separator");
        _child = new("", typeof(ExecutableBlock));
        _slots = [_list, _separator, _child];

        _separator.SetText(", ");

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var items = TextParts.Split(await _list.EvaluateAsync(context), await _separator.EvaluateAsync(context));
        var name = CheckName(_itemName, Fail);

        foreach (var item in items)
        {
            await NextPassAsync(context);
            if (name != null) context.SetParameter(name, item);
            await _child.RunAsync(context);
        }
    }



    // a {name} for each pass, empty for none: letters, digits and _
    private static string? CheckName(string name, Func<string, BlockFailedException> fail)
    {
        var trimmed = name.Trim();
        if (trimmed == "") return null;
        if (!TriggerContext.IsValidName(trimmed)) throw fail($"'{trimmed}' isn't a valid name, use letters, digits, _");
        return trimmed;
    }
}
