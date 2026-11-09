using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using StreamAssist.Components.Blocks.Containers;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Components.Widgets.Actions;
using StreamAssist.Components.Widgets.Background;
using StreamAssist.Components.Widgets.Conditions;
using StreamAssist.Components.Widgets.Containers;
using StreamAssist.Components.Widgets.Model;
using StreamAssist.Components.Widgets.Values;

namespace StreamAssist.Tests;



// engine checks: the Widgets tab: wheels (options, weights, spins and their ticks), mic bounce's curve, saving, the
// widget server and the widget blocks; and the block lists' subgroups
internal static partial class EngineChecks
{
    // ---- widgets: a wheel's options and spins, mic bounce's curve, saving them ----
    private static void WidgetModels()
    {
        var wheel = new WheelWidget();
        Check("wheel: starts with just the empty row, which already has the next colour", wheel.Options.Count == 1 && wheel.FilledOptions.Count == 0
            && wheel.Options[0].Color == WheelWidget.Palette[0]);

        wheel.TryAddOption("Hydrate");
        wheel.TryAddOption("Squats", 3, "#00ff00");
        var added = wheel.TryAddOption("hydrate");
        Check("wheel: options added in order with colours, the same text again is left alone", added && wheel.FilledOptions.Select(o => o.Text).SequenceEqual(["Hydrate", "Squats"])
            && wheel.FilledOptions[0].Color == WheelWidget.Palette[0] && wheel.FilledOptions[1].Color == "#00FF00" && wheel.FilledOptions[1].Weight == 3
            && wheel.Options.Count == 3);
        Check("wheel: a bad weight or colour is refused", !wheel.TryAddOption("X", 0) && !wheel.TryAddOption("X", 1, "orange") && wheel.FilledOptions.Count == 2);

        var slices = WheelWidget.Slices(wheel.FilledOptions);
        Check("wheel: slices are as wide as their weight, clockwise from the top", slices.Count == 2 && slices[0].Start == 0
            && Math.Abs(slices[0].End - 90) < 1e-9 && Math.Abs(slices[1].End - 360) < 1e-9);

        // a spin lands with its winner under the pointer (the pointer reads the slice at -rotation)
        var landed = true;
        var winners = new HashSet<string>();
        for (var i = 0; i < 200; i++)
        {
            var w = new WheelWidget();
            w.TryAddOption("A");
            w.TryAddOption("B", 3);
            string? done = null;
            w.TryStartSpin(winner => done = winner, out _);
            var spin = w.Spin!;
            var at = ((-spin.To % 360) + 360) % 360;
            var slice = spin.Slices[spin.WinnerIndex];
            landed &= at > slice.Start && at < slice.End && spin.To > spin.From + 3 * 360;
            w.FinishSpin()?.Invoke();
            landed &= done == spin.Winner && !w.IsSpinning && w.LastWinner == spin.Winner;
            winners.Add(spin.Winner);
        }
        Check("wheel: a spin turns a few times and lands inside its winner's slice, then gives the winner", landed && winners.SetEquals(["A", "B"]));

        var busy = new WheelWidget();
        busy.TryAddOption("A");
        busy.TryStartSpin(null, out _);
        Check("wheel: spinning again while it spins is quietly nothing, no options is an error",
            !busy.TryStartSpin(null, out var again) && again == "" && !new WheelWidget().TryStartSpin(null, out var none) && none != "");

        var ticks = WheelWidget.TickTimes(busy.Spin!, TimeSpan.FromMilliseconds(45));
        Check("wheel: ticks come faster at first and slow down, never closer than the gap", ticks.Count > 3
            && ticks.Zip(ticks.Skip(1)).All(p => p.Second - p.First >= TimeSpan.FromMilliseconds(45))
            && ticks[^1] - ticks[^2] > ticks[1] - ticks[0] && ticks[^1] <= busy.Spin!.Length);
        busy.FinishSpin();

        wheel.TryRemoveOption("SQUATS");
        Check("wheel: remove by text (case ignored), unknown is an error", wheel.FilledOptions.Count == 1 && !wheel.TryRemoveOption("nope"));
        wheel.ClearOptions();
        Check("wheel: clear empties it", wheel.FilledOptions.Count == 0 && wheel.Options.Count == 1);

        // mic bounce: nothing up to Min, the top from Max, an upside-down parabola between; smoothing 0 jumps straight there
        Check("mic bounce: the curve rests below Min, rises fastest at Min, flattens into the top at Max", MicBounceWidget.Target(-45, -40, -10) == 0
            && Math.Abs(MicBounceWidget.Target(-25, -40, -10) - 0.75) < 1e-9 && MicBounceWidget.Target(0, -40, -10) == 1
            && MicBounceWidget.Target(-37, -40, -10) - MicBounceWidget.Target(-40, -40, -10) > MicBounceWidget.Target(-10, -40, -10) - MicBounceWidget.Target(-13, -40, -10));
        Check("mic bounce: smoothing 0 jumps there, more smoothing follows slower", MicBounceWidget.Smooth(0, 1, 20, 0) == 1
            && MicBounceWidget.Smooth(0, 1, 20, 50) > MicBounceWidget.Smooth(0, 1, 20, 200) && MicBounceWidget.Smooth(0, 1, 20, 50) < 1);
        var talker = new MicBounceWidget(); // Min -40 dB
        talker.Update(-20, 20);
        var talking = talker.IsTalking && talker.Level > 0;
        talker.Update(-60, 100);
        var held = talker.IsTalking;
        talker.Update(-60, 150);
        Check("mic bounce: talking while over Min, held through a short gap, then idle", talking && held && !talker.IsTalking);
        var range = new MicBounceWidget();
        Check("mic bounce: min has to be below max", !range.TrySetRange(-10, -20) && range.TrySetRange(-50, -5) && range.MinDb == -50 && range.MaxDb == -5);

        // a config with both kinds, saved and loaded the way widgets.json is
        var widgets = new WidgetsComponentModel();
        widgets.Configs.TryCreate("Default");
        var config = widgets.Config!;
        config.TryAddWidget(WidgetKind.Wheel, 0, out var saved);
        ((WheelWidget)saved).TryAddOption("Hydrate", 2, "#123456");
        config.TryAddWidget(WidgetKind.MicBounce, 1, out var bounce);
        ((MicBounceWidget)bounce).SetIdleImage("Images/me.png");
        ((MicBounceWidget)bounce).TrySetHeight(80);
        config.TryAddWidget(WidgetKind.Wheel, 2, out _);
        Check("widgets: new ones are named after their kind, numbered when taken", config.Widgets.Select(w => w.Name).SequenceEqual(["Wheel", "Mic Bounce", "Wheel 2"]));
        Check("widgets: names are unique (case ignored), blocks find them by name and kind", !config.TryRenameWidget(config.Widgets[2], "wheel")
            && config.TryRenameWidget(config.Widgets[2], "Big Wheel") && widgets.TryFindWidget("big wheel", WidgetKind.Wheel, out _, out _)
            && !widgets.TryFindWidget("Big Wheel", WidgetKind.MicBounce, out _, out var kindError) && kindError.Contains("isn't a mic bounce"));

        var reloaded = JsonSerializer.Deserialize<WidgetsComponentModel>(JsonSerializer.Serialize(widgets))!;
        var reloadedWheel = reloaded.Config!.Widgets[0] as WheelWidget;
        var reloadedBounce = reloaded.Config!.Widgets[1] as MicBounceWidget;
        Check("widgets: saved and loaded as the right kinds with their settings", reloadedWheel?.FilledOptions.Single() is { Text: "Hydrate", Weight: 2, Color: "#123456" }
            && reloadedBounce is { IdleImage: "Images/me.png", Height: 80, MinDb: -40, MaxDb: -10, Smoothing: 50 } && reloaded.Port == WidgetsComponentModel.DefaultPort);

        Check("widgets: a pasted one gets a free name", config.TryPasteWidget(saved, 3) && config.Widgets[3].Name == "Wheel 2" && config.Widgets[3] != saved);
        widgets.Dispose();
        reloaded.Dispose();
    }



    // ---- plinko: the board's shape, drops, the physics, saving ----
    private static void PlinkoModels()
    {
        var plinko = new PlinkoWidget();
        plinko.TryAddBucket("Big", 2);
        plinko.TryAddBucket("Small");
        plinko.TryAddBucket("Mid");
        var board = plinko.Board;
        double Width(PlinkoBucket b) => b.Right - b.Left;
        Check("plinko: the bottom is split by weight across the walls, a divider between each two", board.Buckets.Count == 3
            && Math.Abs(Width(board.Buckets[0]) - 2 * Width(board.Buckets[1])) < 1e-9 && Math.Abs(board.Buckets[0].Left + PlinkoBoard.WallX(board.Rows - 1)) < 1e-9
            && Math.Abs(board.Buckets[2].Right - PlinkoBoard.WallX(board.Rows - 1)) < 1e-9 && board.Drawn.Count == 5 + 2);
        Check("plinko: a triangle of pins, row r has r + 3 and one on the wall at each end", board.Pins().Count() == Enumerable.Range(0, 12).Sum(r => r + 5)
            && board.Pins().Where(p => p.Y == 4).Max(p => p.X) == PlinkoBoard.WallX(4));

        var each = new List<(string Bucket, int Number)>();
        IReadOnlyList<string>? done = null;
        var doneResult = "";
        var queued = plinko.TryDrop(30, (bucket, number) => each.Add((bucket, number)), (results, result) => (done, doneResult) = (results, result), out _);
        Check("plinko: a drop queues its balls", queued && plinko.IsDropping);

        var inside = true;
        var firstSeen = new Dictionary<int, (double X, double Y)>(); // where each ball dropped in (a step later, so near it)
        for (var i = 0; i < 60 * 50 && plinko.IsDropping; i++)
        {
            plinko.Step(0.02);
            foreach (var ball in plinko.Frame().Balls)
            {
                firstSeen.TryAdd(ball.Id, (ball.X, ball.Y));
                inside &= Math.Abs(ball.X) <= PlinkoBoard.WallX(Math.Clamp(ball.Y, PlinkoBoard.LidY, board.Rows - 1)) + 0.05
                    && ball.Y >= PlinkoBoard.LidY && ball.Y <= board.Floor;
            }
        }
        var names = new[] { "Big", "Small", "Mid" };
        Check("plinko: every ball lands in a bucket and stays inside the board", !plinko.IsDropping && inside && each.All(e => names.Contains(e.Bucket)));
        Check("plinko: balls drop in at random spots in the drop-in area, both sides of it", firstSeen.Count == 30
            && firstSeen.Values.All(p => Math.Abs(p.X) <= PlinkoBoard.SpawnHalfWidth + 0.15 && Math.Abs(p.Y - PlinkoBoard.SpawnY) <= PlinkoBoard.SpawnHalfHeight + 0.15)
            && firstSeen.Values.Any(p => p.X < -0.3) && firstSeen.Values.Any(p => p.X > 0.3));
        // Most: the bucket most landed in, the first to get there on a tie
        string Most(IEnumerable<string> results)
        {
            var counts = new Dictionary<string, int>();
            var best = "";
            foreach (var r in results)
            {
                counts[r] = counts.GetValueOrDefault(r) + 1;
                if (best == "" || counts[r] > counts[best]) best = r;
            }
            return best;
        }
        var showing = plinko.Frame().Result;
        Check("plinko: the drop's result shows once its last ball lands, When Done waits", each.Select(e => e.Number).Order().SequenceEqual(Enumerable.Range(1, 30))
            && done == null && showing != null && showing.Text == Most(each.Select(e => e.Bucket)) && plinko.LastResult == showing.Text && plinko.Frame().Counts.Sum() == 30);

        for (var i = 0; i < 60 * 50 && plinko.IsBusy; i++) plinko.Step(0.02);
        Check("plinko: once the result has gone, When Done gets every bucket in landing order and the result", done != null
            && done.SequenceEqual(each.Select(e => e.Bucket)) && plinko.LastResults.SequenceEqual(done) && doneResult == plinko.LastResult);
        Check("plinko: the balls fade and go with it", plinko.Frame().Balls.Count == 0 && plinko.Frame().Counts.Sum() == 0 && !plinko.HasBalls && plinko.Frame().Result == null);

        var adder = new PlinkoWidget();
        adder.TryAddBucket("Nothing");
        adder.SetMode(PlinkoMode.Sum);
        var notNumber = !adder.TryDrop(1, null, null, out var sumError) && sumError.Contains("'Nothing' isn't a number");
        adder.TryRemoveBucket("Nothing");
        adder.TryAddBucket("10");
        adder.TryAddBucket("2.5");
        adder.TrySetDropTempo(0);
        var sum = "";
        adder.TryDrop(6, null, (_, result) => sum = result, out _);
        for (var i = 0; i < 60 * 50 && adder.IsBusy; i++) adder.Step(0.02);
        var expected = adder.LastResults.Sum(r => double.Parse(r, System.Globalization.CultureInfo.InvariantCulture));
        Check("plinko: Sum adds the buckets up (each a number, or the drop is refused)", notNumber && adder.LastResults.Count == 6
            && sum == expected.ToString("0.##########", System.Globalization.CultureInfo.InvariantCulture));
        adder.Dispose();

        plinko.TrySetDropTempo(1);
        plinko.TryDrop(3, null, null, out _);
        for (var i = 0; i < 25; i++) plinko.Step(0.02);
        var oneIn = plinko.Frame().Balls.Count == 1;
        for (var i = 0; i < 40; i++) plinko.Step(0.02);
        Check("plinko: balls drop in one per drop tempo", oneIn && plinko.Frame().Balls.Count == 2);
        Check("plinko: a new drop waits (quietly) until the last one has finished", !plinko.TryDrop(1, null, null, out var busyError) && busyError == "");
        for (var i = 0; i < 60 * 50 && plinko.IsBusy; i++) plinko.Step(0.02);

        plinko.TrySetMaxBalls(5);
        var overMax = !plinko.TryDrop(6, null, null, out var maxError) && maxError != "";
        var badCount = !plinko.TryDrop(0, null, null, out var countError) && countError != "";
        var empty = new PlinkoWidget();
        var noBuckets = !empty.TryDrop(1, null, null, out var bucketError) && bucketError != "";
        plinko.SetIsDisabled(true);
        var off = !plinko.TryDrop(1, null, null, out var offError) && offError == "";
        plinko.SetIsDisabled(false);
        Check("plinko: over max balls, 0 balls or no buckets is an error; off, quietly nothing", overMax && badCount && noBuckets && off);

        var ghost = new PlinkoWidget();
        ghost.TryAddBucket("A");
        ghost.TryAddBucket("B");
        ghost.SetBallsCollide(false);
        ghost.TrySetDropTempo(0);
        var landed = 0;
        ghost.TryDrop(15, (_, _) => landed++, null, out _);
        for (var i = 0; i < 60 * 50 && ghost.IsDropping; i++) ghost.Step(0.02);
        Check("plinko: balls that don't collide all land too", landed == 15);

        plinko.TrySetRows(8);
        var reloaded = (PlinkoWidget)JsonSerializer.Deserialize<WidgetModel>(JsonSerializer.Serialize<WidgetModel>(plinko))!;
        Check("plinko: saved and loaded, its board built from them", reloaded.Rows == 8 && reloaded.MaxBalls == 5 && reloaded.DropTempo == 1
            && reloaded.Buckets.Select(b => b.Text).SequenceEqual(names) && reloaded.Board.Buckets.Count == 3 && reloaded.Board.Rows == 8);

        plinko.BucketList.TryMove(plinko.Buckets[2], 0);
        Check("plinko: buckets reorder", plinko.Buckets.Select(b => b.Text).SequenceEqual(["Mid", "Big", "Small"]) && plinko.Board.Buckets[0].Text == "Mid");

        foreach (var widget in new WidgetModel[] { plinko, empty, ghost, reloaded }) widget.Dispose();
    }



    // ---- widget blocks: they find their widget by name, Spin Wheel's When Done gets {winner} ----
    private static async Task WidgetBlockChecks()
    {
        var old = StreamAssist.Shell.GlobalContext.WidgetsComponentModel;
        var widgets = new WidgetsComponentModel();
        widgets.Configs.TryCreate("Default");
        widgets.Config!.TryAddWidget(WidgetKind.Wheel, 0, out var found);
        var wheel = (WheelWidget)found;
        StreamAssist.Shell.GlobalContext.WidgetsComponentModel = widgets;
        var ctx = new TriggerContext(TriggerPhase.Once, default, new Dictionary<string, string> { ["user"] = "Bob" });

        T Named<T>(T block, string widget) where T : BlockModel { ((ValueSlot)block.Slots[0]).SetText(widget); return block; }

        var add = Named(new AddWheelOptionAction(), "Wheel");
        ((ValueSlot)add.Slots[1]).SetText("{user}'s pick");
        await add.SafeRunAsync(ctx);
        Check("widget blocks: Add Wheel Option fills in parameters", wheel.FilledOptions.Single().Text == "Bob's pick");
        Check("widget blocks: the Widget dropdown lists that kind's widgets", ((MenuSlot)add.Slots[0]).Options.SequenceEqual(["Wheel"]));

        var spin = Named(new SpinWheelBlock(), "wheel");
        ((BlockSlot)spin.Slots[1]).TrySetBlock(new FormatRecordBlock { Template = "{user} got {winner}" });
        FormatRecordBlock.Log.Clear();
        await spin.SafeRunAsync(ctx);
        var spinning = await Named(new WheelSpinningCondition(), "Wheel").EvaluateAsync(ctx);
        var whenDone = wheel.FinishSpin();
        var notYet = FormatRecordBlock.Log.Count == 0;
        whenDone?.Invoke();
        Check("widget blocks: Spin Wheel spins it, When Done runs with {winner} after the winner has shown", spinning && notYet
            && FormatRecordBlock.Log.SequenceEqual(["Bob got Bob's pick"]) && await Named(new WheelWinnerBlock(), "Wheel").EvaluateAsync(ctx) == "Bob's pick");

        // plinko: Drop Plinko runs When Each Lands per ball and When Done once, with the run's parameters
        widgets.Config!.TryAddWidget(WidgetKind.Plinko, 1, out var foundPlinko);
        var plinko = (PlinkoWidget)foundPlinko;
        plinko.TryAddBucket("10");
        plinko.TryAddBucket("100");
        plinko.TrySetDropTempo(0);
        var drop = Named(new DropPlinkoBlock(), "Plinko");
        ((ValueSlot)drop.Slots[1]).SetText("3");
        ((BlockSlot)drop.Slots[2]).TrySetBlock(new FormatRecordBlock { Template = "{user} ball {ball} in {bucket}" });
        ((BlockSlot)drop.Slots[3]).TrySetBlock(new FormatRecordBlock { Template = "done: {result} of {results}" });
        FormatRecordBlock.Log.Clear();
        await drop.SafeRunAsync(ctx);
        var dropping = await Named(new PlinkoDroppingCondition(), "Plinko").EvaluateAsync(ctx);
        for (var i = 0; i < 60 * 50 && plinko.IsBusy; i++) plinko.Step(0.02);
        await Task.Delay(100); // the When blocks run on their own
        var landings = FormatRecordBlock.Log.Where(l => l.StartsWith("Bob ball")).ToList();
        var results = string.Join(", ", plinko.LastResults);
        var everyBucket = Named(new PlinkoResultsBlock(), "Plinko").Pick("Give", "Every Bucket");
        Check("widget blocks: Drop Plinko runs When Each Lands per ball with {ball} and {bucket}, then When Done with {result} and {results}", dropping
            && landings.Count == 3 && landings.All(l => l.EndsWith(" in 10") || l.EndsWith(" in 100")) && FormatRecordBlock.Log.Last() == $"done: {plinko.LastResult} of {results}"
            && await Named(new PlinkoResultsBlock(), "Plinko").EvaluateAsync(ctx) == plinko.LastResult && await everyBucket.EvaluateAsync(ctx) == results
            && !await Named(new PlinkoDroppingCondition(), "Plinko").EvaluateAsync(ctx));
        var none = Named(new DropPlinkoBlock(), "Plinko");
        ((ValueSlot)none.Slots[1]).SetText("0");
        Check("widget blocks: Drop Plinko with 0 balls fails the run", !await none.SafeRunAsync(ctx));

        var missing = Named(new ClearWheelAction(), "Nope");
        Check("widget blocks: an unknown widget fails the run", !await missing.SafeRunAsync(ctx));

        await Named(new ShowWidgetAction(), "Wheel").SafeRunAsync(ctx);
        var hidden = wheel.IsHidden && !wheel.IsDisabled;
        await add.SafeRunAsync(new TriggerContext(TriggerPhase.Once, default, new Dictionary<string, string> { ["user"] = "Al" }));
        var waitsForWinner = !wheel.TryStartSpin(null, out var waitError) && waitError == "";
        var hiddenSpins = wheel.TryStartSpin(null, out _, DateTime.UtcNow.AddSeconds(wheel.ShowSeconds + 1));
        wheel.FinishSpin();
        Check("widget blocks: a new spin waits (quietly) until the last winner has shown", waitsForWinner);
        Check("widget blocks: Show Widget toggles hidden, a hidden widget still runs (takes changes, spins)", hidden && wheel.FilledOptions.Count == 2 && hiddenSpins);
        var reloadedHidden = JsonSerializer.Deserialize<WidgetsComponentModel>(JsonSerializer.Serialize(widgets))!;
        Check("widget blocks: hidden is saved", reloadedHidden.Config!.Widgets.OfType<WheelWidget>().Single().IsHidden);
        reloadedHidden.Dispose();
        await Named(new ShowWidgetAction(), "Wheel").SafeRunAsync(ctx);
        wheel.SetIsDisabled(true);
        Check("widget blocks: shown again; a widget that's off doesn't spin (quietly)", !wheel.IsHidden && !wheel.TryStartSpin(null, out var offError) && offError == "");

        StreamAssist.Shell.GlobalContext.WidgetsComponentModel = old;
        widgets.Dispose();

        // the block lists: Twitch's blocks in subgroups, its heading counting them all
        var palette = new BlockPaletteViewModel(null, BlockGroup.Twitch, BlockGroup.Widgets);
        var twitch = palette.Groups.Single(g => g.Group == BlockGroup.Twitch);
        Check("subgroups: a group's blocks split into subgroups, its count is all of them",
            twitch.Subgroups.Any(s => s.Title == "Moderation") && twitch.Count == twitch.Cards.Count + twitch.Subgroups.Sum(s => s.Cards.Count)
            && twitch.HeadingText == $"Twitch ({twitch.Count})" && palette.Groups.Single(g => g.Group == BlockGroup.Widgets).Subgroups.Select(s => s.Title).SequenceEqual(["Mic Bounce", "Plinko", "Wheel"]));
        palette.Dispose();
    }



    // ---- widget server: a widget's page, and its live connection (a free port, no OBS) ----
    private static async Task WidgetServerChecks()
    {
        var joined = new TaskCompletionSource<string>();
        using var server = new WidgetServer(name => $"page for {name}", path => path == "Images/a.png" ? (new byte[] { 1, 2, 3 }, "image/png") : null);
        server.Joined += name => joined.TrySetResult(name);

        var probe = new TcpListener(System.Net.IPAddress.Loopback, 0);
        probe.Start();
        var port = ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        Check("widget server: starts on a free port", server.TryStart(port, out _));
        var taken = new WidgetServer(_ => "", _ => null);
        Check("widget server: a port in use says so", !taken.TryStart(port, out var busy) && busy.Contains("in use"));

        using var http = new HttpClient();
        var page = await http.GetStringAsync(WidgetManager.UrlOf(port, "Big Wheel"));
        var asset = await http.GetByteArrayAsync($"http://127.0.0.1:{port}/asset/Images/a.png");
        var nothing = await http.GetAsync($"http://127.0.0.1:{port}/asset/Images/none.png");
        Check("widget server: serves a widget's page (by its name) and its assets", page == "page for Big Wheel" && asset.SequenceEqual(new byte[] { 1, 2, 3 })
            && nothing.StatusCode == System.Net.HttpStatusCode.NotFound);

        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws/Big%20Wheel"), CancellationToken.None);
        var name = await joined.Task.WaitAsync(TimeSpan.FromSeconds(5));
        server.Send("big wheel", "{\"type\":\"state\"}");
        var buffer = new byte[256];
        var received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        Check("widget server: a page's live connection joins by name and gets what's sent to it (case ignored)", name == "Big Wheel"
            && Encoding.UTF8.GetString(buffer, 0, received.Count) == "{\"type\":\"state\"}" && server.OpenNames().SequenceEqual(["Big Wheel"]));

        server.Stop();
        Check("widget server: stopped, it's not running", !server.IsRunning);
    }
}
