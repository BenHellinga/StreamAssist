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
    [JsonIgnore] public override string Description => "Starts a timer and carries on straight away. When it runs out, the blocks in When Done run with this run's parameters as they were at the start. With no Dock it runs in the background, where nothing can see, pause or stop it; naming a timer dock (in the dashboard config in use) puts it there, to watch and control, found by its name. It only lasts while the app runs. If Running says what happens when one with that name is already going. Repeat starts it again each time it runs out (When Done runs each time) until it's stopped, cancelled or the app closes (with no Dock: until the app closes or this block is removed).";



    // private



    private readonly MenuSlot _ifRunning;
    private bool _repeat;
    private readonly ValueSlot<string> _dock;
    private readonly ValueSlot<string> _timerName;
    private readonly ValueSlot<double> _seconds;
    private readonly BlockSlot _whenDone;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



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



    [JsonInclude] [JsonPropertyName("ifRunning")]
    private ValueSlotData JsonIfRunning
    {
        get => _ifRunning.GetData();
        set => _ifRunning.LoadData(value);
    }

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
        _ifRunning = new("If Running", GetIfRunningMenuItems, "Restart");
        _repeat = false;
        _dock = new("Dock");
        _timerName = new("Timer");
        _seconds = new("Seconds");
        _whenDone = new("When Done", typeof(ExecutableBlock));
        _slots = [_ifRunning, _dock, _timerName, _seconds, _whenDone];

        _seconds.SetText("60");

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var ifRunning = await _ifRunning.EvaluateAsync(context);
        var dashboard = GlobalContext.DashboardComponentModel ?? throw Fail("no dashboard");
        var dockName = await _dock.EvaluateAsync(context);
        TimersModel timers;
        if (dockName.Trim() == "") timers = dashboard.BackgroundTimers;
        else if (dashboard.TryFindDock(dockName, DockKind.Timer, out var dock, out var dockError)) timers = dock.Timers;
        else throw Fail(dockError);
        var name = await _timerName.EvaluateAsync(context);
        var seconds = await _seconds.EvaluateAsync(context);
        if (double.IsNaN(seconds) || seconds > 1e9) throw Fail($"can't time {seconds} seconds");

        var repeat = ifRunning switch
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
