using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Values;



// goes in a dropdown (MenuSlot) and picks one of that dropdown's options each run
public class RandomOptionBlock : ValueBlock<string>, IMenuOptionBlock
{
    // info



    [JsonIgnore] public override string Name => "Random Option";
    [JsonIgnore] public override string Description => "Goes in a dropdown and picks one of its options at random each time it runs (never Default or None). Only fits dropdowns.";



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => [];



    // compute



    protected override Task<string> ComputeAsync(TriggerContext context)
    {
        if ((ParentSlot as BlockSlot)?.ValueSlot is not MenuSlot menu) throw Fail("only works in a dropdown");

        var options = menu.RandomOptions;
        if (options.Count == 0) throw Fail($"{menu.Label} has no options to pick from");

        return Task.FromResult(options[Random.Shared.Next(options.Count)]);
    }
}
