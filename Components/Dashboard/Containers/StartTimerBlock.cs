using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Dashboard.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Dashboard.Containers;



public class StartTimerBlock : ContainerBlock
{
    // info



    [JsonIgnore] public override string Name => "Start Timer";
    [JsonIgnore] public override string Description => "Starts a timer and carries on straight away. When it runs out, the blocks in When Done run with this run's parameters as they were at the start. The timer lives on the timer dock named in Dock (in the dashboard config in use), found there by name, and only lasts while the app runs. If Running says what happens when one with that name is already going. Repeat starts it again each time it runs out (When Done runs each time) until it's stopped, cancelled or the app closes.";



    // private



    private string _ifRunning;
    private bool _repeat;
    private readonly ValueSlot<string> _dock;
    private readonly ValueSlot<string> _timerName;
    private readonly ValueSlot<double> _seconds;
    private readonly BlockSlot _whenDone;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("If Running", ConfiguratorType.Menu)] [JsonPropertyName("ifRunning")]
    public string IfRunning
    {
        get => _ifRunning;
        set => SetParameter(ref _ifRunning, value);
    }

    public static List<string> GetIfRunningMenuItems() => ["Restart", "Add Time", "Ignore", "Run Another"];

    // goes round again each time it runs out (When Done each time) until stopped, cancelled or the app closes
    [ConfigurableParameter("Repeat", ConfiguratorType.Boolean)] [JsonPropertyName("repeat")]
    public bool Repeat
    {
        get => _repeat;
        set => SetParameter(ref _repeat, value);
    }

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("dock")]
    private ValueSlotData JsonDock
    {
        get => _dock.GetData();
        set => _dock.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("timer")]
    private ValueSlotData JsonTimerName
    {
        get => _timerName.GetData();
        set => _timerName.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("seconds")]
    private ValueSlotData JsonSeconds
    {
        get => _seconds.GetData();
        set => _seconds.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("whenDone")]
    private PendingBlockData? JsonWhenDone
    {
        get => _whenDone.GetData();
        set => _whenDone.LoadData(value);
    }



    // constructor



    public StartTimerBlock()
    {
        _ifRunning = "Restart";
        _repeat = false;
        _dock = new("Dock");
        _timerName = new("Timer");
        _seconds = new("Seconds");
        _whenDone = new("When Done", typeof(ExecutableBlock));
        _slots = [_dock, _timerName, _seconds, _whenDone];

        _seconds.SetText("60");

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var dashboard = GlobalContext.DashboardComponentModel ?? throw Fail("no dashboard");
        if (!dashboard.TryFindDock(await _dock.EvaluateAsync(context), DockKind.Timer, out var dock, out var dockError)) throw Fail(dockError);
        var timers = dock.Timers;
        var name = await _timerName.EvaluateAsync(context);
        var seconds = await _seconds.EvaluateAsync(context);
        if (double.IsNaN(seconds) || seconds > 1e9) throw Fail($"can't time {seconds} seconds");

        var repeat = _ifRunning switch
        {
            "Add Time" => TimerRepeat.AddTime,
            "Ignore" => TimerRepeat.Ignore,
            "Run Another" => TimerRepeat.RunAnother,
            _ => TimerRepeat.Restart,
        };

        // its own copy of the parameters as they are now, and the binding's cancellation (a removed binding never
        // runs its end)
        var endContext = new TriggerContext(TriggerPhase.Once, context.CancellationToken, context.Parameters);
        if (!timers.TryStart(name, TimeSpan.FromSeconds(seconds), repeat, () => RunWhenDone(endContext), this, out var error, _repeat))
            throw Fail(error);
    }



    private void RunWhenDone(TriggerContext context)
    {
        if (context.CancellationToken.IsCancellationRequested) return;
        if (_whenDone.Block is ExecutableBlock block) _ = block.SafeRunAsync(context);
    }



    // disposal: a removed block's timers go too (there's nothing left to run when they end)



    public override void Dispose()
    {
        GlobalContext.DashboardComponentModel?.KillTimersOwnedBy(this);
        base.Dispose();
    }
}
