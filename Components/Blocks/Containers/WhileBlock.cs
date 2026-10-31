using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Containers;



public class WhileBlock : ContainerBlock
{
    // info



    [JsonIgnore] public override string Name => "While";
    [JsonIgnore] public override string Description => "Runs the block inside again and again while the condition is true (checked before each time, an empty one is false). Put a Wait inside for things that repeat over time, e.g. while a key is held. A loop that keeps going without waiting is stopped after 10000 times. Removing the binding or closing the app stops it.";



    // private



    private const int MaxQuickPasses = 10000;
    private static readonly TimeSpan QuickPass = TimeSpan.FromMilliseconds(5);

    private readonly BlockSlot _condition;
    private readonly BlockSlot _child;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("condition")]
    private PendingBlockData? JsonCondition
    {
        get => _condition.GetData();
        set => _condition.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("child")]
    private PendingBlockData? JsonChild
    {
        get => _child.GetData();
        set => _child.LoadData(value);
    }



    // constructor



    public WhileBlock()
    {
        _condition = new("while", typeof(ConditionBlock));
        _child = new("", typeof(ExecutableBlock));
        _slots = [_condition, _child];

        TrackSlots();
    }



    // execute



    // only passes that didn't wait count towards the limit, so a loop with a Wait inside can run for as long as it's needed
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var quickPasses = 0;
        var clock = new System.Diagnostics.Stopwatch();

        while (true)
        {
            await NextPassAsync(context);
            clock.Restart();

            if (!await ConditionBlock.EvaluateConditionAsync(_condition.Block, context)) return;
            await _child.RunAsync(context);

            quickPasses = clock.Elapsed < QuickPass ? quickPasses + 1 : 0;
            if (quickPasses >= MaxQuickPasses) throw Fail($"stopped after {MaxQuickPasses} times without waiting");
        }
    }
}
