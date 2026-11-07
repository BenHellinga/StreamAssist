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

        var missing = Named(new ClearWheelAction(), "Nope");
        Check("widget blocks: an unknown widget fails the run", !await missing.SafeRunAsync(ctx));

        await Named(new ShowWidgetAction(), "Wheel").SafeRunAsync(ctx);
        var hidden = wheel.IsDisabled;
        await add.SafeRunAsync(new TriggerContext(TriggerPhase.Once, default, new Dictionary<string, string> { ["user"] = "Al" }));
        Check("widget blocks: Show Widget toggles it, a hidden widget still takes changes (it just doesn't run)", hidden && wheel.FilledOptions.Count == 2
            && !wheel.TryStartSpin(null, out var offError) && offError == "");

        StreamAssist.Shell.GlobalContext.WidgetsComponentModel = old;
        widgets.Dispose();

        // the block lists: Twitch's blocks in subgroups, its heading counting them all
        var palette = new BlockPaletteViewModel(null, BlockGroup.Twitch, BlockGroup.Widgets);
        var twitch = palette.Groups.Single(g => g.Group == BlockGroup.Twitch);
        Check("subgroups: a group's blocks split into subgroups, its count is all of them",
            twitch.Subgroups.Any(s => s.Title == "Moderation") && twitch.Count == twitch.Cards.Count + twitch.Subgroups.Sum(s => s.Cards.Count)
            && twitch.HeadingText == $"Twitch ({twitch.Count})" && palette.Groups.Single(g => g.Group == BlockGroup.Widgets).Subgroups.Select(s => s.Title).SequenceEqual(["Mic Bounce", "Wheel"]));
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
