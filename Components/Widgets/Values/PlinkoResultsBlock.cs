using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Widgets.Model;

namespace StreamAssist.Components.Widgets.Values;



public class PlinkoResultsBlock : ValueBlock<string>
{
    // info



    [JsonIgnore] public override string Name => "Plinko Results";
    [JsonIgnore] public override string Description => "The plinko widget named in Widget's last finished drop: its result (the bucket most landed in, or the sum, as the widget's Result is set), or every bucket in the order they landed, e.g. \"10, 100, 10\". Empty until one has finished since the app started. Drop Plinko gives them as {result} and {results} too.";
    [JsonIgnore] public override string? Subgroup => "Plinko";



    // private



    private readonly MenuSlot _widget;
    private readonly MenuSlot _give;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetGiveMenuItems() => ["Result", "Every Bucket"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("widget")]
    private ValueSlotData JsonWidget
    {
        get => _widget.GetData();
        set => _widget.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("give")]
    private ValueSlotData JsonGive
    {
        get => _give.GetData();
        set => _give.LoadData(value);
    }



    // constructor



    public PlinkoResultsBlock()
    {
        _widget = WidgetBlocks.WidgetSlot(WidgetKind.Plinko);
        _give = new("Give", GetGiveMenuItems, "Result");
        _slots = [_widget, _give];

        TrackSlots();
    }



    // compute



    protected override async Task<string> ComputeAsync(TriggerContext context)
    {
        if (!WidgetBlocks.TryFind(await _widget.EvaluateAsync(context), WidgetKind.Plinko, out var found, out var error)) throw Fail(error);
        var plinko = (PlinkoWidget)found;
        return await _give.EvaluateAsync(context) == "Every Bucket" ? string.Join(", ", plinko.LastResults) : plinko.LastResult;
    }
}
