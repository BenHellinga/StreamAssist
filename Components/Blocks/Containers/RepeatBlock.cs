using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Containers;



public class RepeatBlock : ContainerBlock
{
    // info



    [JsonIgnore] public override string Name => "Repeat";
    [JsonIgnore] public override string Description => "Runs the block inside a number of times, one after another. Each time, {i} (or the name in Number As) is which time it is, from 1. Removing the binding or closing the app stops it.";



    // private



    private string _numberName;
    private readonly ValueSlot<int> _times;
    private readonly BlockSlot _child;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Number As", ConfiguratorType.Text)] [JsonPropertyName("numberName")]
    public string NumberName
    {
        get => _numberName;
        set => SetParameter(ref _numberName, value);
    }

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("times")]
    private ValueSlotData JsonTimes
    {
        get => _times.GetData();
        set => _times.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("child")]
    private PendingBlockData? JsonChild
    {
        get => _child.GetData();
        set => _child.LoadData(value);
    }



    // constructor



    public RepeatBlock()
    {
        _numberName = "i";
        _times = new("Times");
        _child = new("", typeof(ExecutableBlock));
        _slots = [_times, _child];

        _times.SetText("3");

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var times = await _times.EvaluateAsync(context);
        if (times < 0) throw Fail($"can't repeat {times} times");
        var name = CheckName(_numberName, Fail);

        for (int i = 1; i <= times; i++)
        {
            await NextPassAsync(context);
            if (name != null) context.SetParameter(name, i.ToString());
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
