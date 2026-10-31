using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Data.Containers;



// runs its blocks at most once every Seconds per Label, remembered in the built-in cooldowns table (so it holds across
// bindings and restarts); while it's cooling down nothing inside runs
public class CooldownBlock : ContainerBlock
{
    // info



    [JsonIgnore] public override string Name => "Cooldown";
    [JsonIgnore] public override string Description => "Runs the blocks inside only if this Label hasn't gone through in the last Seconds, otherwise does nothing. The last time is kept in the cooldowns table, so blocks with the same Label share a cooldown, and {userLogin} in it gives every viewer their own.";
    [JsonIgnore] public override BlockGroup Group => BlockGroup.Control;



    // private



    private readonly ValueSlot<string> _label;
    private readonly ValueSlot<double> _seconds;
    private readonly BlockSlot _ready;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("label")]
    private ValueSlotData JsonLabel
    {
        get => _label.GetData();
        set => _label.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("seconds")]
    private ValueSlotData JsonSeconds
    {
        get => _seconds.GetData();
        set => _seconds.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("ready")]
    private PendingBlockData? JsonReady
    {
        get => _ready.GetData();
        set => _ready.LoadData(value);
    }



    // constructor



    public CooldownBlock()
    {
        _label = new("Label");
        _seconds = new("Seconds");
        _ready = new("", typeof(ExecutableBlock));
        _slots = [_label, _seconds, _ready];

        _seconds.SetText("30");

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var data = GlobalContext.DataModel ?? throw Fail("data not set up");
        var label = (await _label.EvaluateAsync(context)).Trim();
        if (label == "") throw Fail("needs a label");
        var seconds = await _seconds.EvaluateAsync(context);
        if (seconds < 0) throw Fail($"can't cool down for {seconds} seconds");

        if (data.UseCooldown(label, seconds, DateTime.Now, out _)) await _ready.RunAsync(context);
    }
}
