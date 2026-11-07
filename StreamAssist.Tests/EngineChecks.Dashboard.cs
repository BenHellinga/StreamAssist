using StreamAssist.Components.OBS.Model;
using StreamAssist.Components.Spotify.Actions;
using StreamAssist.Components.Spotify.Background;
using StreamAssist.Components.Spotify.Model;
using StreamAssist.Components.Spotify.Values;
using StreamAssist.Components.Data.Model;
using StreamAssist.Components.Data.Background;
using StreamAssist.Components.Data.Actions;
using StreamAssist.Components.Data.Containers;
using StreamAssist.Components.Data.Values;
using System.Net;
using StreamAssist.Components.OBS.Background;
using StreamAssist.Components.OBS.Conditions;
using StreamAssist.Components.OBS.Values;
using StreamAssist.Components.OBS.Actions;
using StreamAssist.Components.OutputConsole.Actions;
using StreamAssist.Components.Input.Conditions;
using StreamAssist.Components.Audio.Actions;
using StreamAssist.Core.Connections;
using StreamAssist.Components.Chatbot.Values;
using StreamAssist.Components.Chatbot.Actions;
using StreamAssist.Components.Blocks.Values;
using StreamAssist.Components.Blocks.Actions;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Components.Chatbot.Background;
using StreamAssist.Components.Chatbot.ChatBlocks;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Components.Blocks.Conditions;
using StreamAssist.Components.Input.Background;
using StreamAssist.Components.Input.Model;
using StreamAssist.Components.Input.UI;
using System.Text.Json;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Blocks.Containers;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Components.Dashboard.Model;

namespace StreamAssist.Tests;



// engine checks: dashboard docks, their layout and the blocks that change them
internal static partial class EngineChecks
{
    // ---- Dashboard: docks, their layout, Dock Message (main thread, errors are counted) ----
    private static void Dashboard()
    {
        int Errors() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Count(l => l.Contains("] error - ")); }
        string LastError() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Last(l => l.Contains("] error - ")); }
        var dash = new StreamAssist.Components.Dashboard.Model.DashboardComponentModel();
        dash.Configs.TryCreate("Default");

        // the config bar: picking another config doesn't rebuild the dropdown's list (that blanked its selection)
        dash.Configs.TryCreate("Second");
        var bar = new StreamAssist.Components.Blocks.UI.ConfigBarViewModel(dash.Configs);
        var listChanges = 0;
        bar.Names.CollectionChanged += (_, _) => listChanges++;
        bar.SelectedName = "Default";
        Check("config bar: picking a config switches it and keeps the list as it is", dash.Configs.SelectedName == "Default" && bar.SelectedName == "Default" && listChanges == 0);
        dash.Configs.TryDelete("Second");
        Check("config bar: a deleted config leaves the list", bar.Names.SequenceEqual(["Default"]) && listChanges > 0);
        bar.Dispose();
        var cfg = dash.Config!;
        // the layout as text: a dock's name, or H(...) side by side / V(...) stacked
        string Shape(StreamAssist.Components.Dashboard.Model.DockLayoutNode? n) => n == null ? "-"
            : n.IsDock ? cfg.Docks.First(d => d.Id == n.DockId).Name
            : (n.Direction == StreamAssist.Components.Dashboard.Model.SplitDirection.Horizontal ? "H(" : "V(") + string.Join(",", n.Children.Select(Shape)) + ")";
        StreamAssist.Components.Dashboard.Model.DockModel D(string name) => cfg.FindDock(name)!;
        const StreamAssist.Components.Dashboard.Model.DockKind Console = StreamAssist.Components.Dashboard.Model.DockKind.Console;

        Check("dashboard: starts with no docks", Shape(cfg.Layout) == "-");
        Check("dashboard: new docks go on the right", cfg.TryAddDock("A", Console) && Shape(cfg.Layout) == "A" && cfg.TryAddDock("B", Console) && Shape(cfg.Layout) == "H(A,B)");
        var d0 = Errors();
        Check("dashboard: names are needed and unique (case ignored), one error each", !cfg.TryAddDock(" ", Console) && !cfg.TryAddDock("a", Console) && Errors() == d0 + 2
            && LastError().Contains("dock 'a' already exists") && !cfg.TryRenameDock(D("B"), "A") && D("B").Name == "B" && cfg.TryRenameDock(D("B"), "b") && D("b").Name == "b");
        cfg.TryRenameDock(D("b"), "B");
        Check("dashboard: dropped below a dock, that region splits", cfg.TryMoveDock(D("B"), D("A"), StreamAssist.Components.Dashboard.Model.DockEdge.Bottom) && Shape(cfg.Layout) == "V(A,B)");
        cfg.TryAddDock("C", Console);
        Check("dashboard: a new one goes right of everything", Shape(cfg.Layout) == "H(V(A,B),C)");
        Check("dashboard: dropped above a dock in a column, it joins the column taking half the target's share",
            cfg.TryMoveDock(D("C"), D("A"), StreamAssist.Components.Dashboard.Model.DockEdge.Top) && Shape(cfg.Layout) == "V(C,A,B)" && cfg.Layout!.Sizes.SequenceEqual([0.5, 0.5, 1.0]));
        Check("dashboard: onto itself changes nothing", cfg.TryMoveDock(D("A"), D("A"), StreamAssist.Components.Dashboard.Model.DockEdge.Left) && Shape(cfg.Layout) == "V(C,A,B)");
        Check("dashboard: removing one closes the gap", cfg.TryRemoveDock(D("A")) && Shape(cfg.Layout) == "V(C,B)" && cfg.Docks.Count == 2);
        Check("dashboard: a split left with one region becomes it", cfg.TryMoveDock(D("B"), D("C"), StreamAssist.Components.Dashboard.Model.DockEdge.Left) && Shape(cfg.Layout) == "H(B,C)");
        Check("dashboard: a card dropped on a dock's side adds one there, named after its kind",
            cfg.TryAddDock(Console, D("B"), StreamAssist.Components.Dashboard.Model.DockEdge.Bottom) && Shape(cfg.Layout) == "H(V(B,Console),C)");
        Check("dashboard: the next one of that kind gets a number", cfg.TryAddDock(Console) && Shape(cfg.Layout) == "H(V(B,Console),C,Console 2)");
        cfg.TryRemoveDock(D("Console"));
        cfg.TryRemoveDock(D("Console 2"));
        cfg.SetSizes(cfg.Layout!, [300, 100]);
        Check("dashboard: splitter sizes are kept as shares, bad ones ignored", cfg.Layout!.Sizes.SequenceEqual([300.0, 100.0]) && SetSizesIgnored(cfg));
        static bool SetSizesIgnored(StreamAssist.Components.Dashboard.Model.DashboardConfigModel c) { c.SetSizes(c.Layout!, [1, 0]); c.SetSizes(c.Layout!, [1]); return c.Layout!.Sizes.SequenceEqual([300.0, 100.0]); }

        // saved and loaded (a config copy goes through the same json)
        var reloaded = System.Text.Json.JsonSerializer.Deserialize<StreamAssist.Components.Dashboard.Model.DashboardComponentModel>(System.Text.Json.JsonSerializer.Serialize(dash))!;
        var reCfg = reloaded.Config!;
        Check("dashboard: saved and loaded keeps docks, layout and sizes", reCfg.Docks.Select(d => d.Name).SequenceEqual(["B", "C"])
            && reCfg.Layout!.Children.Select(c => c.DockId).SequenceEqual(cfg.Docks.Select(d => d.Id)) && reCfg.Layout.Sizes.SequenceEqual([300.0, 100.0]));
        var broken = System.Text.Json.JsonSerializer.Deserialize<StreamAssist.Components.Dashboard.Model.DashboardConfigModel>(
            "{\"name\":\"x\",\"docks\":[{\"id\":\"1\",\"name\":\"One\",\"kind\":\"Console\"},{\"id\":\"2\",\"name\":\"Two\",\"kind\":\"Console\"}],"
            + "\"layout\":{\"direction\":\"Vertical\",\"children\":[{\"dock\":\"1\"},{\"dock\":\"gone\"}],\"sizes\":[1,1]}}")!;
        Check("dashboard: a loaded layout is mended (unknown region dropped, a missing dock added on the right)",
            broken.Layout!.Direction == StreamAssist.Components.Dashboard.Model.SplitDirection.Horizontal && broken.Layout.Children.Select(c => c.DockId).SequenceEqual(["1", "2"]));
        Check("dashboard: a new config copies the docks", dash.Configs.TryCreate("Copy") && dash.Config!.Docks.Select(d => d.Name).SequenceEqual(["B", "C"]) && dash.Configs.TrySelect("Default"));

        // the blocks write to the config in use, found by name
        var oldDash = StreamAssist.Shell.GlobalContext.DashboardComponentModel;
        StreamAssist.Shell.GlobalContext.DashboardComponentModel = dash;
        var dockCtx = new TriggerContext(TriggerPhase.Once, default, new Dictionary<string, string> { ["user"] = "Bob" });
        StreamAssist.Components.Dashboard.Actions.DockMessageAction Msg(string dock, string text)
        {
            var b = new StreamAssist.Components.Dashboard.Actions.DockMessageAction();
            ((ValueSlot)b.Slots[0]).SetText(dock);
            ((ValueSlot)b.Slots[1]).SetText(text);
            return b;
        }
        Msg("c", "{user} redeemed").SafeRunAsync(dockCtx).GetAwaiter().GetResult();
        Check("dock message: written to that dock only, not the app console", D("C").Lines.Count == 1 && D("C").Lines[0].EndsWith("] Bob redeemed") && D("B").Lines.Count == 0
            && !AppLog.Lines.Any(l => l.Contains("Bob redeemed")));
        var d1 = Errors();
        Msg("Nope", "hi").SafeRunAsync(dockCtx).GetAwaiter().GetResult();
        Check("dock message: an unknown dock fails with one error naming it", Errors() == d1 + 1 && LastError().Contains("error - Dock Message: no dock 'Nope'"));
        var clear = new StreamAssist.Components.Dashboard.Actions.ClearDockAction();
        ((ValueSlot)clear.Slots[0]).SetText("C");
        clear.SafeRunAsync(dockCtx).GetAwaiter().GetResult();
        Check("clear dock: empties it", D("C").Lines.Count == 0);

        // button docks: rows like redeem rows, each pressing its own binding
        var btnDock = (cfg.TryAddDock(StreamAssist.Components.Dashboard.Model.DockKind.Button) ? D("Buttons") : null)!;
        var rows = btnDock.Buttons;
        Check("button dock: starts with one empty row, naming it adds the next", rows.Rows.Count == 1 && NameRow(rows.Rows[0], "Reset Bruce") && rows.Rows.Count == 2);
        static bool NameRow(StreamAssist.Components.Dashboard.Model.DockButton row, string name) { row.SetName(name); return true; }
        var bruce = rows.Rows[0];
        var bruceBind = new BindableModel();
        bruceBind.TryAddBlock(new RecordBlock { Tag = "bruce" });
        RecordBlock.Log.Clear();
        Check("button dock: pressing runs its binding", bruce.TryBind(bruceBind) && btnDock.TryPressButton(bruce) && RecordBlock.Log.SequenceEqual(["bruce"]));
        var btnJson = System.Text.Json.JsonSerializer.Serialize(cfg);
        var btnCopy = System.Text.Json.JsonSerializer.Deserialize<StreamAssist.Components.Dashboard.Model.DashboardConfigModel>(btnJson)!;
        Check("button dock: rows and bindings are saved (consoles save no buttons)", btnCopy.FindDock("Buttons")!.Buttons.Rows.Count == 2
            && btnCopy.FindDock("Buttons")!.Buttons.Rows[0].Bindable?.HasBlock == true && !System.Text.RegularExpressions.Regex.IsMatch(btnJson, "\"name\":\"B\"[^}]*\"buttons\""));
        btnCopy.Dispose();

        // reordering (dragging a row onto another): the row goes before the one it lands on, the empty one stays last
        string Order() => string.Join(",", rows.Rows.Select(r => r.Name == "" ? "_" : r.Name));
        NameRow(rows.Rows[^1], "Hydrate");
        NameRow(rows.Rows[^1], "Lurk");
        Check("button dock: a row moves before the one it's dropped on", rows.TryMoveRow(rows.Rows[2], 0) && Order() == "Lurk,Reset Bruce,Hydrate,_");
        Check("button dock: dropped on the empty row it goes last, binding and all", rows.TryMoveRow(bruce, 3) && Order() == "Lurk,Hydrate,Reset Bruce,_"
            && bruce.Bindable == bruceBind);
        var lurkBind = new BindableModel();
        lurkBind.TryAddBlock(new RecordBlock { Tag = "lurk" });
        rows.Rows[0].TryBind(lurkBind);
        RecordBlock.Log.Clear();
        btnDock.PressAllButtons();
        Check("button dock: press all runs every bound button, top to bottom", RecordBlock.Log.SequenceEqual(["lurk", "bruce"]));
        rows.Rows[0].Unbind();
        var m0 = Errors();
        Check("button dock: the empty row can't move (one error)", !rows.TryMoveRow(rows.Rows[^1], 0) && Order() == "Lurk,Hydrate,Reset Bruce,_" && Errors() == m0 + 1);
        NameRow(rows.Rows[0], "");
        NameRow(rows.Rows[0], "");
        rows.Remove(bruce);
        Check("button dock: deleting a row drops it and its binding", rows.Rows.Count == 1 && rows.Rows[0].IsEmpty && bruce.Bindable == null);
        cfg.TryRemoveDock(btnDock);

        // timers, with a clock the checks move by hand
        var clock = new DateTime(2026, 1, 1);
        var tm = new StreamAssist.Components.Dashboard.Model.TimersModel(() => clock);
        var ended = new List<string>();
        bool Start(string name, double s, StreamAssist.Components.Dashboard.Model.TimerRepeat r, string tag) =>
            tm.TryStart(name, TimeSpan.FromSeconds(s), r, () => ended.Add(tag), null, out _);
        const StreamAssist.Components.Dashboard.Model.TimerRepeat Restart = StreamAssist.Components.Dashboard.Model.TimerRepeat.Restart;
        Check("timers: a name and a positive length are needed", !tm.TryStart(" ", TimeSpan.FromSeconds(5), Restart, null, null, out var tErr1) && tErr1 == "timer needs a name"
            && !tm.TryStart("x", TimeSpan.Zero, Restart, null, null, out _) && tm.Timers.Count == 0);
        Start("egg", 10, Restart, "egg1");
        clock = clock.AddSeconds(4);
        Start("egg", 10, Restart, "egg2");
        Check("timers: Restart starts the one running again (its new end replaces the old)", tm.Timers.Count == 1 && tm.Timers[0].LeftAt(clock) == TimeSpan.FromSeconds(10));
        Start("egg", 5, StreamAssist.Components.Dashboard.Model.TimerRepeat.AddTime, "egg3");
        Start("egg", 99, StreamAssist.Components.Dashboard.Model.TimerRepeat.Ignore, "egg4");
        Check("timers: Add Time adds to it, Ignore leaves it", tm.Timers.Count == 1 && tm.Timers[0].LeftAt(clock) == TimeSpan.FromSeconds(15) && tm.Timers[0].Total == TimeSpan.FromSeconds(15));
        Start("egg", 3, StreamAssist.Components.Dashboard.Model.TimerRepeat.RunAnother, "other");
        Check("timers: Run Another runs one more with the same name", tm.Find("EGG").Count() == 2);
        tm.SetIsPaused(tm.Timers[0], true);
        clock = clock.AddSeconds(5);
        tm.FinishDue();
        Check("timers: due ones finish and run their end, a paused one keeps its time", ended.SequenceEqual(["other"]) && tm.Timers.Count == 1 && tm.Timers[0].LeftAt(clock) == TimeSpan.FromSeconds(15));
        tm.SetIsPaused(tm.Timers[0], false);
        clock = clock.AddSeconds(15);
        tm.FinishDue();
        Check("timers: resumed, it ends after what it had left (with the end of the latest restart)", ended.SequenceEqual(["other", "egg2"]) && tm.Timers.Count == 0);
        Start("a", 5, Restart, "a");
        Start("b", 5, Restart, "b");
        tm.Kill(tm.Find("a").First());
        tm.Finish(tm.Find("b").First());
        Check("timers: cancel doesn't run its end, finish now does", ended.SequenceEqual(["other", "egg2", "b"]) && tm.Timers.Count == 0);
        ended.Clear();
        Start("late", 9, Restart, "late");
        Start("soon", 2, Restart, "soon");
        tm.SetAllPaused(true);
        Check("timers: pause all", tm.Timers.All(t => t.IsPaused));
        tm.SetAllPaused(false);
        tm.FinishAll();
        Check("timers: finish all runs every end, soonest first", tm.Timers.Count == 0 && ended.SequenceEqual(["soon", "late"]));
        Start("x", 5, Restart, "x");
        tm.KillAll();
        Check("timers: cancel all runs no end", tm.Timers.Count == 0 && ended.Count == 2);
        ended.Clear();
        tm.TryStart("lap", TimeSpan.FromSeconds(5), Restart, () => ended.Add("lap"), null, out _, repeats: true);
        clock = clock.AddSeconds(5);
        tm.FinishDue();
        Check("timers: a repeating one runs its end and starts its next lap", ended.SequenceEqual(["lap"]) && tm.Timers.Count == 1 && tm.Timers[0].LeftAt(clock) == TimeSpan.FromSeconds(5));
        clock = clock.AddSeconds(12);
        tm.FinishDue();
        Check("timers: one that fell behind starts its next lap from now", ended.Count == 2 && tm.Timers[0].LeftAt(clock) == TimeSpan.FromSeconds(5));
        tm.AddTime(tm.Timers[0], TimeSpan.FromSeconds(10));
        Check("timers: add time", tm.Timers[0].LeftAt(clock) == TimeSpan.FromSeconds(15));
        tm.AddTimeAll(TimeSpan.FromSeconds(-20));
        tm.FinishDue();
        Check("timers: taking more than is left ends this lap now", ended.Count == 3 && tm.Timers.Count == 1);
        tm.AddTime(tm.Timers[0], TimeSpan.FromSeconds(2));
        tm.Finish(tm.Timers[0]);
        Check("timers: finishing a repeating one early runs its end and starts a fresh lap", ended.Count == 4 && tm.Timers.Count == 1
            && tm.Timers[0].LeftAt(clock) == TimeSpan.FromSeconds(5));
        tm.Kill(tm.Timers[0]);
        Check("timers: only killing a repeating one ends it (without its end)", ended.Count == 4 && tm.Timers.Count == 0);

        // a dock turned off takes no changes (quietly), a blank dock takes a pasted copy of another
        {
            cfg.TryAddDock(StreamAssist.Components.Dashboard.Model.DockKind.Console);
            var console = cfg.Docks.Last();
            console.SetIsDisabled(true);
            console.WriteLine("hi");
            Check("docks: a turned-off console takes no messages", console.IsDisabled && console.Lines.Count == 0);
            console.SetIsDisabled(false);
            console.WriteLine("hi");
            Check("docks: back on it does", console.Lines.Count == 1);

            cfg.TryAddDock(StreamAssist.Components.Dashboard.Model.DockKind.Timer);
            var offTimers = cfg.Docks.Last();
            offTimers.SetIsDisabled(true);
            Check("docks: a turned-off timer dock starts nothing, and says nothing",
                offTimers.Timers.TryStart("x", TimeSpan.FromSeconds(5), Restart, null, null, out var offError) && offError == "" && offTimers.Timers.Timers.Count == 0);

            cfg.TryAddDock(StreamAssist.Components.Dashboard.Model.DockKind.Indicator);
            var lights = cfg.Docks.Last();
            lights.Indicators.Rows[0].SetName("Mic");
            lights.ToggleAllIndicators();
            Check("docks: toggle all turns indicators never set on", lights.IndicatorState("Mic") == true);
            lights.SetIsDisabled(true);
            lights.ToggleAllIndicators();
            Check("docks: a turned-off indicator dock doesn't change", lights.IndicatorState("Mic") == true
                && lights.TrySetIndicator("Nope", false, out var lightsError) && lightsError == "");

            cfg.TryAddDock(StreamAssist.Components.Dashboard.Model.DockKind.Blank);
            var blank = cfg.Docks.Last();
            var blankId = blank.Id;
            blank.SetIsDisabled(true);
            Check("docks: a blank dock can't be turned off", !blank.IsDisabled);
            Check("docks: a dock pasted over a blank one takes its place and id, under a free name",
                cfg.TryPasteDock(blank, console) && cfg.Docks.FirstOrDefault(d => d.Id == blankId) is { } pastedDock
                && pastedDock != console && pastedDock.Kind == StreamAssist.Components.Dashboard.Model.DockKind.Console
                && pastedDock.Name == console.Name + " 2" && !cfg.Docks.Any(d => d.Kind == StreamAssist.Components.Dashboard.Model.DockKind.Blank));
            Check("docks: only a blank dock takes a paste", !cfg.TryPasteDock(console, lights));

            foreach (var dock in new[] { console, offTimers, lights }.Concat(cfg.Docks.Where(d => d.Id == blankId)).ToList()) cfg.TryRemoveDock(dock);
        }

        // the timer blocks, on a timer dock named in their Dock slot
        var timerDock = (cfg.TryAddDock(StreamAssist.Components.Dashboard.Model.DockKind.Timer) ? D("Timers") : null)!;
        var startTimer = new StreamAssist.Components.Dashboard.Containers.StartTimerBlock();
        ((ValueSlot)startTimer.Slots[1]).SetText("Timers");
        ((ValueSlot)startTimer.Slots[2]).SetText("Hydrate");
        ((ValueSlot)startTimer.Slots[3]).SetText("90");
        ((BlockSlot)startTimer.Slots[4]).TrySetBlock(new FormatRecordBlock { Template = "{user} drinks" });
        var timerCtx = new TriggerContext(TriggerPhase.Once, default, new Dictionary<string, string> { ["user"] = "Bob" });
        startTimer.SafeRunAsync(timerCtx).GetAwaiter().GetResult();
        timerCtx.SetParameter("user", "Changed");
        var leftBlock = new StreamAssist.Components.Dashboard.Values.TimerLeftBlock();
        ((ValueSlot)leftBlock.Slots[0]).SetText("timers");
        ((ValueSlot)leftBlock.Slots[1]).SetText("hydrate");
        Check("start timer: starts it on its dock, Timer Left gives the seconds left", timerDock.Timers.Find("Hydrate").Count() == 1 && leftBlock.EvaluateAsync(timerCtx).GetAwaiter().GetResult() == 90);
        // no Dock: it runs in the background, on no dock, and still runs When Done when it ends
        var bgTimer = new StreamAssist.Components.Dashboard.Containers.StartTimerBlock();
        ((ValueSlot)bgTimer.Slots[2]).SetText("Background");
        ((ValueSlot)bgTimer.Slots[3]).SetText("0.01");
        ((BlockSlot)bgTimer.Slots[4]).TrySetBlock(new FormatRecordBlock { Template = "{user} ended" });
        bgTimer.SafeRunAsync(timerCtx).GetAwaiter().GetResult();
        var bgStarted = dash.BackgroundTimers.Find("Background").Count() == 1 && !timerDock.Timers.Find("Background").Any();
        FormatRecordBlock.Log.Clear();
        Thread.Sleep(30);
        dash.FinishDueTimers();
        Check("start timer: no Dock runs it in the background, When Done still runs", bgStarted && !dash.BackgroundTimers.Timers.Any() && FormatRecordBlock.Log.SequenceEqual(["Changed ended"]));
        bgTimer.Dispose();
        var pauseTimer = new StreamAssist.Components.Dashboard.Actions.PauseTimerAction();
        ((ValueSlot)pauseTimer.Slots[1]).SetText("Timers");
        ((ValueSlot)pauseTimer.Slots[2]).SetText("Hydrate");
        pauseTimer.SafeRunAsync(timerCtx).GetAwaiter().GetResult();
        Check("pause timer: Toggle pauses it", timerDock.Timers.Find("Hydrate").Single().IsPaused);
        var stopTimer = new StreamAssist.Components.Dashboard.Actions.StopTimerAction();
        ((ValueSlot)stopTimer.Slots[1]).SetText("Timers");
        ((ValueSlot)stopTimer.Slots[2]).SetText("Hydrate");
        FormatRecordBlock.Log.Clear();
        stopTimer.SafeRunAsync(timerCtx).GetAwaiter().GetResult();
        Check("stop timer: Finish Now runs When Done with the parameters from the start", FormatRecordBlock.Log.SequenceEqual(["Bob drinks"]) && !timerDock.Timers.Find("Hydrate").Any()
            && leftBlock.EvaluateAsync(timerCtx).GetAwaiter().GetResult() == 0);
        var t0 = Errors();
        ((ValueSlot)stopTimer.Slots[1]).SetText("Timerz");
        Check("timer blocks: an unknown dock fails with one error", !stopTimer.SafeRunAsync(timerCtx).GetAwaiter().GetResult() && Errors() == t0 + 1
            && LastError().Contains("error - Stop Timer: no dock 'Timerz'"));
        ((ValueSlot)stopTimer.Slots[1]).SetText("C");
        Check("timer blocks: a dock of another kind fails too", !stopTimer.SafeRunAsync(timerCtx).GetAwaiter().GetResult() && Errors() == t0 + 2
            && LastError().Contains("dock 'C' isn't a timer dock"));
        startTimer.SafeRunAsync(timerCtx).GetAwaiter().GetResult();
        startTimer.Dispose();
        Check("start timer: removing the block drops its timers", !timerDock.Timers.Find("Hydrate").Any());
        timerDock.Timers.TryStart("left", TimeSpan.FromSeconds(5), StreamAssist.Components.Dashboard.Model.TimerRepeat.Restart, () => RecordBlock.Log.Add("ran"), null, out _);
        RecordBlock.Log.Clear();
        cfg.TryRemoveDock(timerDock);
        Check("timer dock: removing it drops its timers without running their end", timerDock.Timers.Timers.Count == 0 && RecordBlock.Log.Count == 0);

        // indicators: an indicator dock's rows, on / off / toggle by dock and row name (case ignored), unknown until set
        var indDock = (cfg.TryAddDock(StreamAssist.Components.Dashboard.Model.DockKind.Indicator) ? D("Indicators") : null)!;
        indDock.Indicators.Rows[^1].SetName("Mic");
        indDock.Indicators.Rows[^1].SetName("Cam");
        var indView = new StreamAssist.Components.Dashboard.UI.IndicatorDockViewModel(indDock);
        Check("indicators: unknown until set", indDock.IndicatorState("Mic") == null && !indView.Rows[0].IsOn && !indView.Rows[0].IsOff);
        var setInd = new StreamAssist.Components.Dashboard.Actions.SetIndicatorAction().Pick("Set", "Toggle");
        ((ValueSlot)setInd.Slots[1]).SetText("indicators");
        ((ValueSlot)setInd.Slots[2]).SetText("Mic");
        setInd.SafeRunAsync(dockCtx).GetAwaiter().GetResult();
        Check("set indicator: Toggle turns a new one on, and its row shows it", indDock.IndicatorState(" mic ") == true && indView.Rows[0].IsOn && !indView.Rows[1].IsOn);
        setInd.Pick("Set", "Off");
        setInd.SafeRunAsync(dockCtx).GetAwaiter().GetResult();
        var isOn = new StreamAssist.Components.Dashboard.Conditions.IndicatorIsOnCondition();
        ((ValueSlot)isOn.Slots[0]).SetText("Indicators");
        ((ValueSlot)isOn.Slots[1]).SetText("MIC");
        Check("set indicator: Off, and Indicator Is On reads it", indDock.IndicatorState("Mic") == false && indView.Rows[0].IsOff && !isOn.EvaluateAsync(dockCtx).GetAwaiter().GetResult());
        setInd.Pick("Set", "On");
        setInd.SafeRunAsync(dockCtx).GetAwaiter().GetResult();
        Check("indicator is on: true once on", isOn.EvaluateAsync(dockCtx).GetAwaiter().GetResult());
        var i0 = Errors();
        ((ValueSlot)setInd.Slots[2]).SetText("Mik");
        Check("set indicator: a name with no row fails with one error (no new indicator)", !setInd.SafeRunAsync(dockCtx).GetAwaiter().GetResult() && Errors() == i0 + 1
            && LastError().Contains("error - Set Indicator: no indicator 'Mik' on dock 'Indicators'") && indDock.IndicatorState("Mik") == null);
        ((ValueSlot)isOn.Slots[1]).SetText("Mik");
        Check("indicator is on: a name with no row fails too", !isOn.EvaluateAsync(dockCtx).ContinueWith(t => t.IsFaulted ? false : t.Result).GetAwaiter().GetResult() && Errors() == i0 + 2);
        indView.Dispose();
        var indCopy = System.Text.Json.JsonSerializer.Deserialize<StreamAssist.Components.Dashboard.Model.DashboardConfigModel>(System.Text.Json.JsonSerializer.Serialize(cfg))!;
        Check("indicator dock: row names are saved", indCopy.FindDock("Indicators")!.Indicators.Rows.Select(r => r.Name).SequenceEqual(["Mic", "Cam", ""]));
        indCopy.Dispose();
        cfg.TryRemoveDock(indDock);
        StreamAssist.Shell.GlobalContext.DashboardComponentModel = oldDash;
    }



    // ---- sound docks: one at a time in order, stop / skip, a turned-off dock plays nothing (pretend sounds) ----
    private static void SoundQueue()
    {
        var played = new List<string>();
        var stopped = new List<string>();
        var sounds = new SoundsModel();

        FakeSound Fake(string name) => new(name, played, stopped);
        QueuedSound Add(FakeSound fake) => sounds.Enqueue(fake.Name, Task.FromResult<ISoundPlayback?>(fake))!;

        var a = Fake("a"); var b = Fake("b"); var c = Fake("c");
        var qa = Add(a); var qb = Add(b); Add(c);
        Check("sound dock: only the first plays, the rest wait in order", played.SequenceEqual(["a"]) && qa.IsPlaying && !qb.IsPlaying
            && sounds.Sounds.Select(q => q.Label).SequenceEqual(["a", "b", "c"]));

        a.Finish();
        Check("sound dock: when one ends the next plays", played.SequenceEqual(["a", "b"]) && qa.Done.IsCompleted && sounds.Sounds.Count == 2);

        sounds.Stop(sounds.Sounds[1]);
        Check("sound dock: stopping a waiting one takes it out (freed, never played)", stopped.Contains("c") && sounds.Sounds.Count == 1 && played.Count == 2);

        sounds.StopPlaying();
        Check("sound dock: skip stops the playing one", stopped.Contains("b") && sounds.Sounds.Count == 0 && qb.Done.IsCompleted);

        // a sound still being made holds its place: the ones after it wait for it
        var making = new TaskCompletionSource<ISoundPlayback?>();
        var slow = Fake("slow"); var after = Fake("after");
        sounds.Enqueue("slow", making.Task);
        Add(after);
        var waited = played.Count == 2;
        making.SetResult(slow);
        Check("sound dock: speech still being made keeps its place", waited && played.SequenceEqual(["a", "b", "slow"]));

        // one that couldn't be made is skipped
        sounds.StopAll();
        var broken = sounds.Enqueue("broken", Task.FromResult<ISoundPlayback?>(null));
        Add(Fake("next"));
        Check("sound dock: one that couldn't be made is skipped", broken != null && broken.Done.IsCompleted && played.Last() == "next");

        sounds.StopAll();
        Check("sound dock: stop all empties it", sounds.Sounds.Count == 0 && stopped.Contains("next") && stopped.Contains("after"));

        // a turned-off dock: nothing is queued (freed), stop does nothing
        var dock = DockModel.Create("Sounds", DockKind.Sound);
        dock.Sounds.Enqueue("on", Task.FromResult<ISoundPlayback?>(Fake("on")));
        dock.SetIsDisabled(true);
        var off = Fake("off");
        var queuedOff = dock.Sounds.Enqueue("off", Task.FromResult<ISoundPlayback?>(off));
        dock.Sounds.StopAll();
        Check("sound dock: off, nothing is queued and stop does nothing", queuedOff == null && stopped.Contains("off") && !played.Contains("off")
            && dock.Sounds.Sounds.Count == 1);
        dock.Dispose();
        Check("sound dock: a removed dock stops its sounds", stopped.Contains("on"));

        // not waiting until done: each plays straight away, over the others; turned back on, new ones wait for them
        var together = new SoundsModel();
        together.SetWaitUntilDone(false);
        played.Clear();
        together.Enqueue("x", Task.FromResult<ISoundPlayback?>(Fake("x")));
        together.Enqueue("y", Task.FromResult<ISoundPlayback?>(Fake("y")));
        Check("sound dock: without Wait Until Done they play over each other", played.SequenceEqual(["x", "y"]) && together.Sounds.All(q => q.IsPlaying));
        together.SetWaitUntilDone(true);
        together.Enqueue("z", Task.FromResult<ISoundPlayback?>(Fake("z")));
        var zWaited = !played.Contains("z");
        together.StopPlaying();
        Check("sound dock: turned back on, a new one waits for the ones playing", zWaited && played.Last() == "z");
        together.Dispose();

        // pause: a playing one holds where it is (and, waiting until done, holds the ones after it); a waiting one can't
        var held = new SoundsModel();
        var first = Fake("first");
        var qFirst = held.Enqueue("first", Task.FromResult<ISoundPlayback?>(first))!;
        var qSecond = held.Enqueue("second", Task.FromResult<ISoundPlayback?>(Fake("second")))!;
        held.SetPaused(qSecond, true);
        held.SetAllPaused(true);
        var pausedOk = first.Paused && qFirst.IsPaused && !qSecond.IsPaused && !played.Contains("second");
        held.SetAllPaused(false);
        Check("sound dock: pause holds a playing one (and the queue), pause all resumes them all, a waiting one isn't paused",
            pausedOk && !first.Paused && !qFirst.IsPaused);
        held.Dispose();

        var savedDock = DockModel.Create("Sounds", DockKind.Sound);
        savedDock.Sounds.SetWaitUntilDone(false);
        var reloadedDock = System.Text.Json.JsonSerializer.Deserialize<DockModel>(System.Text.Json.JsonSerializer.Serialize(savedDock))!;
        Check("sound dock: Wait Until Done is saved", !reloadedDock.Sounds.WaitUntilDone && new DockModel().Sounds.WaitUntilDone);
        savedDock.Dispose();
        reloadedDock.Dispose();
    }



    private sealed class FakeSound(string name, List<string> played, List<string> stopped) : ISoundPlayback
    {
        private Action? _finished;

        public string Name => name;
        public bool Paused { get; private set; }
        public void Start(Action finished) { _finished = finished; played.Add(name); }
        public void Pause() => Paused = true;
        public void Resume() => Paused = false;
        public void Stop() { stopped.Add(name); var f = _finished; _finished = null; f?.Invoke(); }
        public void Finish() { var f = _finished; _finished = null; f?.Invoke(); }
    }
}
