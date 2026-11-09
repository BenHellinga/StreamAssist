using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Widgets.Model;

namespace StreamAssist.Components.Widgets.Containers;



public class DropPlinkoBlock : ContainerBlock
{
    // info



    [JsonIgnore] public override string Name => "Drop Plinko";
    [JsonIgnore] public override string Description => "Drops Balls balls into the plinko widget named in Widget and carries on straight away (they drop in one at a time). As each ball lands, When Each Lands runs with this run's parameters as they were at the start, plus {bucket} and {ball} (its number in this drop). Once they all have, the result shows in front of the board for its Result Time; once it's gone, When Done runs with {result} (the bucket most landed in, or the sum, as the widget's Result is set) and {results} (every bucket, in the order they landed). The last drop not finished yet (its When Done not run), or turned off: nothing happens.";
    [JsonIgnore] public override string? Subgroup => "Plinko";



    // private



    private readonly MenuSlot _widget;
    private readonly ValueSlot<int> _balls;
    private readonly BlockSlot _whenEachLands;
    private readonly BlockSlot _whenDone;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("widget")]
    private ValueSlotData JsonWidget
    {
        get => _widget.GetData();
        set => _widget.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("balls")]
    private ValueSlotData JsonBalls
    {
        get => _balls.GetData();
        set => _balls.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("whenEachLands")]
    private PendingBlockData? JsonWhenEachLands
    {
        get => _whenEachLands.GetData();
        set => _whenEachLands.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("whenDone")]
    private PendingBlockData? JsonWhenDone
    {
        get => _whenDone.GetData();
        set => _whenDone.LoadData(value);
    }



    // constructor



    public DropPlinkoBlock()
    {
        _widget = WidgetBlocks.WidgetSlot(WidgetKind.Plinko);
        _balls = new("Balls");
        _balls.SetText("1");
        _whenEachLands = new("When Each Lands", typeof(ExecutableBlock));
        _whenDone = new("When Done", typeof(ExecutableBlock));
        _slots = [_widget, _balls, _whenEachLands, _whenDone];

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        if (!WidgetBlocks.TryFind(await _widget.EvaluateAsync(context), WidgetKind.Plinko, out var found, out var error)) throw Fail(error);
        var plinko = (PlinkoWidget)found;
        var balls = await _balls.EvaluateAsync(context);

        // the parameters as they are now, and the binding's cancellation (a removed binding never runs its ends)
        var start = new TriggerContext(TriggerPhase.Once, context.CancellationToken, context.Parameters);
        if (!plinko.TryDrop(balls, (bucket, ball) => RunWhenEachLands(start, bucket, ball), (results, result) => RunWhenDone(start, results, result), out var dropError)
            && dropError != "") throw Fail(dropError);
    }



    // each landing runs with its own copy, so two balls landing close together don't share {bucket}
    private void RunWhenEachLands(TriggerContext start, string bucket, int ball)
    {
        if (start.CancellationToken.IsCancellationRequested || _whenEachLands.Block is not ExecutableBlock block) return;

        var context = new TriggerContext(TriggerPhase.Once, start.CancellationToken, start.Parameters);
        context.SetParameter("bucket", bucket);
        context.SetParameter("ball", ball.ToString());
        _ = block.SafeRunAsync(context);
    }



    private void RunWhenDone(TriggerContext start, IReadOnlyList<string> results, string result)
    {
        if (start.CancellationToken.IsCancellationRequested || _whenDone.Block is not ExecutableBlock block) return;

        var context = new TriggerContext(TriggerPhase.Once, start.CancellationToken, start.Parameters);
        context.SetParameter("results", string.Join(", ", results));
        context.SetParameter("result", result);
        _ = block.SafeRunAsync(context);
    }
}
