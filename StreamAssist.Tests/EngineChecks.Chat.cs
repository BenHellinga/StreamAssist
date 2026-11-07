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

namespace StreamAssist.Tests;



// engine checks: the chatbot: commands, chat blocks and matching, redeems and events, eventsub parsing, the twitch action queue
internal static partial class EngineChecks
{
    // helpers



    // a target is filled with either a binding or a chat block
    private static void Fill(ChatTarget target, object then)
    {
        if (then is BindableModel bindable) target.TryBind(bindable);
        else target.Child.TrySetBlock((ChatBlock)then);
    }



    private static MatchTextChatBlock M(params (string Pattern, object Then)[] rows)
    {
        var block = new MatchTextChatBlock();
        foreach (var (pattern, then) in rows)
        {
            var row = block.Rows.Rows[^1]; // the trailing empty row
            row.SetPattern(pattern);
            Fill(row.Target, then);
        }
        return block;
    }



    private static ParameterChatBlock P(string name, string reads, object then) { var b = new ParameterChatBlock { ParameterName = name, Reads = reads }; Fill(b.Target, then); return b; }



    private static BindableModel B(string tag) { var b = new BindableModel(); b.TryAddBlock(new ChatLogBlock { Tag = tag }); return b; }



    // ---- editable commands, two roots, Has Permission (on the main thread, errors are counted) ----
    private static void EditableCommands()
    {
        int Errors() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Count(l => l.Contains("] error - ")); }
        string LastError() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Last(l => l.Contains("] error - ")); }
        BindableModel Bind(BlockModel root) { var b = new BindableModel(); b.TryAddBlock(root); return b; }
        Dictionary<string, string> Chat(bool mod = false, bool vip = false, bool sub = false, bool broadcaster = false)
        {
            var p = ChatEvents.SampleChatParameters("viewer", "", mod, vip, sub);
            if (broadcaster) p["isBroadcaster"] = "true";
            return p;
        }

        // protected: "#" -> Has Permission (mod) -> Match Text [x -> rec X], "!clash" -> rec P
        // editable: Match Text [!lurk -> send, !sub -> Match Text [a -> rec A], !rec -> rec R, !clash -> rec E, /.*/ -> anything else]
        var model = Chatbot();
        var top = (MatchTextChatBlock)model.Root.Block!;
        var custom = (MatchTextChatBlock)model.EditableRoot.Block!;
        MatchTextRow? Cmd(string pattern) => custom.Rows.Rows.FirstOrDefault(r => r.Pattern == pattern);
        var lurkSend = new SendChatMessageAction(); lurkSend.Message.SetText("LURK");
        custom.Rows.AddRow("!lurk").Target.TryBind(Bind(lurkSend));
        var subMenu = new MatchTextChatBlock();
        subMenu.Rows.AddRow("a").Target.TryBind(Bind(new RecordBlock { Tag = "A" }));
        custom.Rows.AddRow("!sub").Target.Child.TrySetBlock(subMenu);
        custom.Rows.AddRow("!rec").Target.TryBind(Bind(new RecordBlock { Tag = "R" }));
        custom.Rows.AddRow("!clash").Target.TryBind(Bind(new RecordBlock { Tag = "E" }));
        var anyRow = custom.Rows.AddRow("/.*/"); anyRow.Target.TryBind(Bind(new ParamRecordBlock { Key = "user" })); // catches everything else, records nothing
        var permission = new HasPermissionChatBlock { Level = "Moderator" };
        var admin = new MatchTextChatBlock();
        var xTarget = admin.Rows.AddRow("x").Target;
        xTarget.TryBind(Bind(new RecordBlock { Tag = "X" }));
        permission.Target.Child.TrySetBlock(admin);
        top.Rows.AddRow("#").Target.Child.TrySetBlock(permission);
        top.Rows.AddRow("!clash").Target.TryBind(Bind(new RecordBlock { Tag = "P" }));

        string Send(string text, Dictionary<string, string> p)
        {
            RecordBlock.Log.Clear();
            model.TryMatchMessage(new ChatMatchState(text, 0, p));
            return string.Join("", RecordBlock.Log);
        }

        // the roots
        Check("roots: a fresh chatbot has an empty Match Text in the custom root", Chatbot().EditableRoot.Block is MatchTextChatBlock);
        Check("roots: the custom root only takes a Match Text", !model.EditableRoot.Accepts(new HasPermissionChatBlock()) && !model.EditableRoot.Accepts(new ParameterChatBlock()));
        Check("roots: built-in wins over custom", Send("!clash", Chat()) == "P");

        // matching
        Check("custom: plain row matches the whole word", Send("!rec", Chat()) == "R" && Send("!REC and more", Chat()) == "R");
        Check("custom: plain row doesn't match part of a word", Send("!recording", Chat()) == "");
        Check("permission: moderator passes", Send("#x", Chat(mod: true)) == "X");
        Check("permission: higher level passes too", Send("#x", Chat(broadcaster: true)) == "X");
        Check("permission: lower levels are stopped", Send("#x", Chat(vip: true, sub: true)) == "");
        Check("permission: no chatter is stopped", Send("#x", new Dictionary<string, string>()) == "");
        Check("permission: path goes through it", model.Describe(xTarget)?.Text == "#x");
        permission.Level = "Subscriber";
        Check("permission: vip counts as subscriber", Send("#x", Chat(vip: true)) == "X" && Send("#x", Chat()) == "");
        permission.Level = "VIP";
        Check("permission: sub isn't vip", Send("#x", Chat(sub: true)) == "" && Send("#x", Chat(mod: true)) == "X");
        permission.Level = "Moderator";

        // add
        var e1 = Errors();
        Check("add: whole command", model.TryAddCommand("!hello", "  hi there ") && Cmd("!hello")?.Target.Bindable?.Root.Blocks.FirstOrDefault() is SendChatMessageAction { Message.Text: "hi there" });
        Check("add: no errors so far", Errors() == e1);
        Check("add: a symbol at the end is fine", model.TryAddCommand("!hi?", "x") && Cmd("!hi?") != null && model.TryDeleteCommand("!HI?"));
        Check("add: duplicate refused (any case)", !model.TryAddCommand("!HELLO", "x") && LastError().Contains("error - can't add !HELLO, already exists"));
        Check("add: no name refused", !model.TryAddCommand("  ", "x") && LastError().Contains("command, no name"));
        Check("add: two words refused", !model.TryAddCommand("!a b", "x") && LastError().Contains("must be one word"));
        Check("add: empty response refused", !model.TryAddCommand("!new", "  ") && LastError().Contains("no response"));
        var e2 = Errors();
        Check("add: quiet when asked", !model.TryAddCommand("!hello", "x", logErrors: false) && Errors() == e2);
        Check("add: exactly one empty row at the end", custom.Rows.Rows.Count(r => r.IsEmpty) == 1 && custom.Rows.Rows[^1].IsEmpty);
        Check("add: bind box shows !hello", model.Describe(Cmd("!hello")!.Target)?.Text == "!hello");

        // edit
        Check("edit: send-only command", model.TryEditCommand("!hello", "new text") && ((SendChatMessageAction)Cmd("!hello")!.Target.Bindable!.Root.Blocks.FirstOrDefault()!).Message.Text == "new text");
        Check("edit: a command that does more is refused", !model.TryEditCommand("!rec", "x") && LastError().Contains("not just a message"));
        Check("edit: a row leading into blocks is refused", !model.TryEditCommand("!sub", "x") && LastError().Contains("not a single command"));
        Check("edit: missing refused", !model.TryEditCommand("!nope", "x") && LastError().Contains("can't edit !nope, not found"));

        // delete
        Check("delete: a prefix isn't a command", !model.TryDeleteCommand("!") && Cmd("!lurk") != null);
        Check("delete: a regex row isn't a command", !model.TryDeleteCommand(".*") && !model.TryDeleteCommand("!anything") && anyRow.Target.Bindable != null);
        Check("edit: a plain typed row is a command", model.TryEditCommand("!LURK", "lurking") && lurkSend.Message.Text == "lurking");
        Check("delete: a row leading into blocks is refused", !model.TryDeleteCommand("!sub") && Cmd("!sub") != null);
        Check("delete: built-in commands can't be touched", !model.TryDeleteCommand("#x") && !model.TryDeleteCommand("#") && xTarget.Bindable != null);
        var rowsBefore = custom.Rows.Rows.Count;
        Check("delete: whole command", model.TryDeleteCommand("!hello") && Cmd("!hello") == null && custom.Rows.Rows.Count == rowsBefore - 1);
        Check("delete: still one empty row at the end", custom.Rows.Rows.Count(r => r.IsEmpty) == 1 && custom.Rows.Rows[^1].IsEmpty);

        // the action blocks: quiet from chat, logged elsewhere, and a failure stops what comes after
        StreamAssist.Shell.GlobalContext.ChatbotComponentModel = model;
        string RunAdd(string command, Dictionary<string, string> p)
        {
            RecordBlock.Log.Clear();
            var list = new ListBlock();
            ((BlockSlotList)list.Slots[0]).TryInsertBlock(new AddCommandAction(), 0);
            ((BlockSlotList)list.Slots[0]).TryInsertBlock(new RecordBlock { Tag = "ok" }, 1);
            p["command"] = command; p["content"] = "reply";
            list.SafeRunAsync(new TriggerContext(TriggerPhase.Once, default, p)).GetAwaiter().GetResult();
            return string.Join("", RecordBlock.Log);
        }
        var e3 = Errors();
        Check("action: adds and carries on", RunAdd("!fresh", Chat()) == "ok" && Cmd("!fresh") != null && Errors() == e3);
        Check("action: from chat fails quietly and stops", RunAdd("!fresh", Chat()) == "" && Errors() == e3);
        Check("action: from a key fails with one error", RunAdd("!fresh", new Dictionary<string, string>()) == "" && Errors() == e3 + 1);
        StreamAssist.Shell.GlobalContext.ChatbotComponentModel = null;

        // no Match Text (removed from the editable root)
        var bare = Chatbot();
        bare.EditableRoot.RemoveBlock(bare.EditableRoot.Block!);
        Check("no block: refused", !bare.TryAddCommand("!a", "b") && LastError().Contains("no custom Match Text"));

        // save round trip
        var savedJson = JsonSerializer.Serialize(model);
        var loadedModel = JsonSerializer.Deserialize<ChatbotComponentModel>(savedJson)!;
        Check("round trip: json stable", JsonSerializer.Serialize(loadedModel) == savedJson);
        Check("round trip: custom commands still work", loadedModel.TryAddCommand("!again", "x") && !loadedModel.TryAddCommand("!fresh", "x"));
        var emptyEditable = JsonSerializer.Deserialize<ChatbotComponentModel>(JsonSerializer.Serialize(bare))!;
        Check("round trip: a removed Match Text stays removed", emptyEditable.EditableRoot.Block == null);
        var noConfig = JsonSerializer.Deserialize<ChatbotComponentModel>("{}")!;
        Check("no config: a fresh chatbot has none, so nothing runs or can be bound", noConfig.ChatConfigs.SelectedName == null && noConfig.Root == null
            && noConfig.EventsConfigs.SelectedName == null && noConfig.Redeems == null && noConfig.Events.Count == 0
            && !noConfig.TryMatchMessage(new ChatMatchState("!x", 0, Chat())) && !noConfig.TryRunEvent("raid", Chat())
            && !noConfig.TryAddCommand("!x", "y", logErrors: false) && !new ChatbotComponentModel().TryRequestCreateReward("Hydrate", "100", "", false));
        Check("no config: the first one starts with empty Match Texts", noConfig.ChatConfigs.TryCreate("First") && noConfig.Root!.Block is MatchTextChatBlock
            && noConfig.EditableRoot!.Block is MatchTextChatBlock && noConfig.EventsConfigs.TryCreate("First") && noConfig.Redeems != null);
        model = loadedModel;
        Check("round trip: permission still gates", Send("#x", Chat()) == "" && Send("#x", Chat(mod: true)) == "X");

        // Has Permission: Anyone lets everyone past
        var levels = Chatbot();
        var gate = new HasPermissionChatBlock { Level = "Anyone" };
        gate.Target.TryBind(Bind(new RecordBlock { Tag = "A" }));
        ((MatchTextChatBlock)levels.Root.Block!).Rows.AddRow("?").Target.Child.TrySetBlock(gate);
        RecordBlock.Log.Clear();
        levels.TryMatchMessage(new ChatMatchState("?", 0, Chat()));
        Check("has permission: Anyone lets everyone through", string.Join("", RecordBlock.Log) == "A"
            && HasPermissionChatBlock.GetLevelMenuItems().SequenceEqual(new[] { "Anyone", "Subscriber", "VIP", "Moderator", "Broadcaster" }));

        // configs: each holds both trees, switching swaps them, a new one starts as a copy of the one in use
        var configs = Chatbot();
        configs.TryAddCommand("!normal", "hi");
        Check("chat configs: a new one is a copy of the one in use, switched to", configs.ChatConfigs.TryCreate("April Fools") && configs.ChatConfigs.SelectedName == "April Fools"
            && configs.TryDeleteCommand("!normal"));
        Check("chat configs: names are unique (case ignored) and needed", !configs.ChatConfigs.TryCreate("april fools") && !configs.ChatConfigs.TryCreate(" "));
        configs.ChatConfigs.TrySelect("Default");
        Check("chat configs: switching back brings its own tree", configs.TryEditCommand("!normal", "hello"));
        var configsLoaded = JsonSerializer.Deserialize<ChatbotComponentModel>(JsonSerializer.Serialize(configs))!;
        Check("chat configs: saved and loaded with the one in use", configsLoaded.ChatConfigs.Names.SequenceEqual(new[] { "Default", "April Fools" })
            && configsLoaded.ChatConfigs.SelectedName == "Default" && !configsLoaded.TryAddCommand("!normal", "x", logErrors: false));
        configs.Redeems.AddRow("Hydrate");
        Check("events configs: a copy has the redeems too, deleting the one in use switches back", configs.EventsConfigs.TryCreate("Quiet") && configs.Redeems.Rows.Any(r => r.Title == "Hydrate")
            && configs.EventsConfigs.TryDelete("Quiet") && configs.EventsConfigs.SelectedName == "Default");
        Check("configs: deleting the last one leaves none", configs.EventsConfigs.TryDelete("Default") && configs.EventsConfigs.SelectedName == null && configs.Redeems == null
            && configs.ChatConfigs.TryDelete("Default") && configs.ChatConfigs.TryDelete("April Fools") && configs.Root == null
            && JsonSerializer.Deserialize<ChatbotComponentModel>(JsonSerializer.Serialize(configs))!.ChatConfigs.SelectedName == null);
    }



    // ---- editable commands under a prefix row (Editable: Match Text [! -> Match Text [lurk -> send]]) ----
    private static void EditableCommandsUnderPrefix()
    {
        var m = Chatbot();
        var editable = (MatchTextChatBlock)m.EditableRoot.Block!;
        var bangMenu = new MatchTextChatBlock();
        editable.Rows.AddRow("!").Target.Child.TrySetBlock(bangMenu);
        var lurk = new SendChatMessageAction(); lurk.Message.SetText("LURK LURK LURK!!!");
        var lb = new BindableModel(); lb.TryAddBlock(lurk); bangMenu.Rows.AddRow("lurk").Target.TryBind(lb);
        string Patterns(MatchTextChatBlock b) => string.Join(" | ", b.Rows.Rows.Where(r => r.Pattern != "").Select(r => r.Pattern));

        Check("nested: !editcom !lurk edits the message", m.TryEditCommand("!lurk", "bruh") && lurk.Message.Text == "bruh");
        Check("nested: case is ignored", m.TryEditCommand("!LURK", "bruh2") && lurk.Message.Text == "bruh2");
        Check("nested: add goes under the ! row", m.TryAddCommand("!hello", "hi") && Patterns(bangMenu) == "lurk | hello" && Patterns(editable) == "!");
        Check("nested: the added one shows the full command", m.Describe(bangMenu.Rows.Rows[1].Target)?.Text == "!hello");
        Check("nested: it runs from chat", m.TryAddCommand("!rec", "x") && Patterns(bangMenu) == "lurk | hello | rec");
        Check("nested: no matching prefix adds at the top", m.TryAddCommand("?x", "y") && Patterns(editable) == "! | ?x");
        Check("nested: the prefix row itself isn't a command", !m.TryDeleteCommand("!") && !m.TryEditCommand("!", "z") && !m.TryAddCommand("!", "z"));
        Check("nested: already exists under the prefix", !m.TryAddCommand("!LURK", "z"));
        Check("nested: delete removes it from the nested Match Text", m.TryDeleteCommand("!hello") && Patterns(bangMenu) == "lurk | rec");
        Check("nested: a missing one doesn't exist", !m.TryDeleteCommand("!nope") && !m.TryEditCommand("!nope", "z"));
        CommandFailure F(Func<CommandFailure> f) => f();
        Check("failure: edit missing is NotFound", F(() => { m.TryEditCommand("!nope", "z", out var f, logErrors: false); return f; }) == CommandFailure.NotFound);
        Check("failure: delete a prefix is NotFound", F(() => { m.TryDeleteCommand("!", out var f, logErrors: false); return f; }) == CommandFailure.NotFound);
        Check("failure: add existing is AlreadyExists", F(() => { m.TryAddCommand("!lurk", "z", out var f, logErrors: false); return f; }) == CommandFailure.AlreadyExists);
        var fancy = new BindableModel(); fancy.TryAddBlock(new ListBlock()); bangMenu.Rows.AddRow("fancy").Target.TryBind(fancy);
        Check("failure: edit a command that does more is NotSimple", F(() => { m.TryEditCommand("!fancy", "z", out var f, logErrors: false); return f; }) == CommandFailure.NotSimple);
        Check("failure: no name is Invalid", F(() => { m.TryAddCommand(" ", "z", out var f, logErrors: false); return f; }) == CommandFailure.Invalid);
        Check("failure: success is None", F(() => { m.TryEditCommand("!lurk", "ok", out var f); return f; }) == CommandFailure.None);
        var editableJson = JsonSerializer.Serialize(m);
        var m2 = JsonSerializer.Deserialize<ChatbotComponentModel>(editableJson)!;
        Check("nested: works after save/load", m2.TryEditCommand("!lurk", "again") && m2.TryDeleteCommand("!rec"));
    }



    // ---- plain text rows vs /regex/ rows ----
    private static void PlainTextAndRegexRows()
    {
        var m = Chatbot();
        var top = (MatchTextChatBlock)m.Root.Block!;
        var bang = new MatchTextChatBlock();
        top.Rows.AddRow("!").Target.Child.TrySetBlock(bang);
        BindableModel Rec(string tag) { var b = new BindableModel(); b.TryAddBlock(new RecordBlock { Tag = tag }); return b; }
        BindableModel Seen(string key) { var b = new BindableModel(); b.TryAddBlock(new ParamRecordBlock { Key = key }); return b; }
        bang.Rows.AddRow("lurk").Target.TryBind(Rec("L"));
        var payMenu = new MatchTextChatBlock();
        var allTarget = payMenu.Rows.AddRow("all").Target; allTarget.TryBind(Rec("ALL"));
        bang.Rows.AddRow("pay").Target.Child.TrySetBlock(payMenu);
        bang.Rows.AddRow("(?<x>a)").Target.TryBind(Rec("LIT"));
        var any = bang.Rows.AddRow("//"); any.Target.TryBind(Seen("rest"));
        var greedy = new MatchTextChatBlock(); var greedyTarget = greedy.Rows.AddRow("/.*/").Target; greedyTarget.TryBind(Seen("rest"));
        top.Rows.AddRow("?").Target.Child.TrySetBlock(greedy);
        string Send(string text) { RecordBlock.Log.Clear(); ParamRecordBlock.Seen = null; m.TryMatchMessage(new ChatMatchState(text, 0, new Dictionary<string, string>())); return string.Join("", RecordBlock.Log) + (ParamRecordBlock.Seen is { } s ? $"[{s}]" : ""); }

        Check("plain: whole word", Send("!lurk") == "L" && Send("!lurk ahaa") == "L" && Send("!LURK!") == "L");
        Check("plain: not part of a word", Send("!lurker") == "[lurker]");
        Check("plain: a symbol joins onto the next word", Send("! lurk") == "[ lurk]");
        Check("plain: spaces between words are skipped", Send("!pay all") == "ALL" && Send("!pay   all") == "ALL");
        Check("plain: two words glued together don't match", Send("!payall") == "[payall]");
        Check("plain: regex characters are just text", Send("!(?<x>a)") == "LIT" && Send("!a") == "[a]");
        Check("//: matches nothing, so anything left lands there with it in rest", Send("!blah") == "[blah]");
        Check("/.*/: takes the rest, so rest is empty", Send("?anything here") == "[]");
        Check("display: words get a space, // adds nothing", m.Describe(allTarget)?.Text == "!pay all" && m.Describe(any.Target)?.Text == "!");
        Check("display: a regex shows as written", m.Describe(greedyTarget)?.Text == "?/.*/");
        Check("a /regex/ row is a regex, plain text isn't", any.IsRegex && !bang.Rows.Rows[0].IsRegex);

        // a parameter right after a symbol reads straight away
        var m2 = Chatbot();
        var p = new ParameterChatBlock { ParameterName = "cmd" }; p.Target.TryBind(Seen("cmd"));
        ((MatchTextChatBlock)m2.Root.Block!).Rows.AddRow("!").Target.Child.TrySetBlock(p);
        string Cmd(string text) { ParamRecordBlock.Seen = null; m2.TryMatchMessage(new ChatMatchState(text, 0, new Dictionary<string, string>())); return ParamRecordBlock.Seen ?? "(none)"; }
        Check("param: after a symbol it reads right away", Cmd("!bob") == "bob" && Cmd("! bob") == "(none)");
        Check("param: shown glued to a symbol", m2.Describe(p.Target)?.Text == "!{cmd}");

        // editable commands only ever touch plain rows
        var e = Chatbot();
        var editable = (MatchTextChatBlock)e.EditableRoot.Block!;
        var catchAll = editable.Rows.AddRow("//"); catchAll.Target.TryBind(Rec("X"));
        var regexRow = editable.Rows.AddRow("/!r.*/"); regexRow.Target.TryBind(Rec("R"));
        Check("custom: a regex row isn't a command", !e.TryDeleteCommand("//", logErrors: false) && !e.TryDeleteCommand("/!r.*/", logErrors: false) && !e.TryDeleteCommand("!run", logErrors: false) && catchAll.Target.Bindable != null && regexRow.Target.Bindable != null);
        Check("custom: a command in slashes is refused", !e.TryAddCommand("/x/", "y", out var f, logErrors: false) && f == CommandFailure.Invalid);
        Check("custom: added commands are plain words", e.TryAddCommand("!hi", "yo") && editable.Rows.Rows.Any(r => r.Pattern == "!hi"));
    }



    // ---- Math, reward settings, Get / Set Reward Setting, New reward (main thread, errors are counted) ----
    private static void MathAndRewardSettings()
    {
        int Errors() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Count(l => l.Contains("] error - ")); }
        string LastError() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Last(l => l.Contains("] error - ")); }
        var redeem = new TriggerContext(TriggerPhase.Once, default, new Dictionary<string, string> { ["cost"] = "100", ["rewardId"] = "abc" });
        object Eval(ValueBlock block) => block.EvaluateObjectAsync(redeem).GetAwaiter().GetResult();
        var e0 = Errors();
        // reward settings: read from and write to twitch's reward json
        var rewardJson = JsonDocument.Parse("""{"id":"abc","title":"Hydrate","prompt":"drink","cost":100,"is_enabled":true,"is_paused":false,"is_user_input_required":false,"should_redemptions_skip_request_queue":true,"background_color":"#9147FF","global_cooldown_setting":{"is_enabled":true,"global_cooldown_seconds":60},"max_per_stream_setting":{"is_enabled":false,"max_per_stream":0},"max_per_user_per_stream_setting":{"is_enabled":true,"max_per_user_per_stream":2}}""").RootElement;
        object Read(string name) => RewardSettings.Find(name)!.Read(rewardJson);
        Check("reward json: plain values", Read("Cost") is 100 && Read("Title") is "Hydrate" && Read("Enabled") is true && Read("Skip request queue") is true && Read("Background color") is "#9147FF");
        Check("reward json: limits read 0 when off", Read("Cooldown seconds") is 60 && Read("Max per stream") is 0 && Read("Max per viewer per stream") is 2);
        string Write(string name, object value) { var o = new System.Text.Json.Nodes.JsonObject(); RewardSettings.Find(name)!.Write(o, value); return o.ToJsonString(); }
        Check("reward json: write cost", Write("Cost", 101) == """{"cost":101}""");
        Check("reward json: 0 turns a limit off", Write("Cooldown seconds", 0) == """{"is_global_cooldown_enabled":false}""");
        Check("reward json: a limit turns on with its value", Write("Max per stream", 5) == """{"is_max_per_stream_enabled":true,"max_per_stream":5}""");
        Check("reward json: every setting has a matching kind", RewardSettings.All.All(s => ValueTypes.ClrType(s.Kind).IsInstanceOfType(s.Read(rewardJson))));

        // get / set blocks without a chatbot: fail with one error each, the run stops
        var e1 = Errors();
        var getFailed = false;
        try { Eval(new GetRewardSettingBlock()); } catch (BlockFailedException) { getFailed = true; }
        Check("get reward setting: no chatbot fails with one error", getFailed && Errors() == e1 + 1);
        var get = new GetRewardSettingBlock().Pick("Setting", "Title");
        Check("get reward setting: picking a setting picks its type", get.Kind == ValueKind.Text);
        get.Pick("Setting", "Paused");
        Check("get reward setting: bool setting gives true/false", get.Kind == ValueKind.Bool);
        var getLoaded = (GetRewardSettingBlock)BlockResolver.DeepCopy(new GetRewardSettingBlock { As = "Text" }.Pick("Setting", "Cost"))!;
        Check("get reward setting: a saved type is kept", getLoaded.Picked("Setting") == "Cost" && getLoaded.Kind == ValueKind.Text);
        var set = new SetRewardSettingAction();
        ((ValueSlot)set.Slots[2]).BlockSlot.TrySetBlock(new ExpressionBlock());
        ((ExpressionBlock)((ValueSlot)set.Slots[2]).BlockSlot.Block!).Formula.SetText("{cost}+1");
        var setList = new ListBlock();
        ((BlockSlotList)setList.Slots[0]).TryInsertBlock(set, 0);
        ((BlockSlotList)setList.Slots[0]).TryInsertBlock(new RecordBlock { Tag = "after" }, 1);
        RecordBlock.Log.Clear();
        var e2 = Errors();
        setList.SafeRunAsync(redeem).GetAwaiter().GetResult();
        Check("set reward setting: no chatbot fails with one error and stops", Errors() == e2 + 1 && RecordBlock.Log.Count == 0);
        Check("set reward setting: starts on the reward just redeemed", ((ValueSlot)new SetRewardSettingAction().Slots[1]).Text == "{reward}");

        // new reward: the model checks it, then asks the background
        var chatbot = Chatbot();
        NewReward? asked = null;
        chatbot.CreateRewardRequested += r => asked = r;
        var e3 = Errors();
        Check("new reward: valid one is asked for", chatbot.TryRequestCreateReward(" Make this cost more ", " 100 ", " go ", true)
            && asked == new NewReward("Make this cost more", 100, "go", true) && Errors() == e3);
        asked = null;
        Check("new reward: no title refused", !chatbot.TryRequestCreateReward("  ", "100", "", false) && asked == null && LastError().Contains("reward needs a title"));
        Check("new reward: bad cost refused", !chatbot.TryRequestCreateReward("x", "0", "", false) && !chatbot.TryRequestCreateReward("x", "lots", "", false) && LastError().Contains("must be 1 or more"));
        Check("new reward: long title refused", !chatbot.TryRequestCreateReward(new string('a', 46), "1", "", false) && LastError().Contains("over 45 characters"));

        // its redeem row: added once, an existing one (any case) is reused
        var first = chatbot.Redeems.AddRow("Make this cost more");
        var again = chatbot.Redeems.AddRow(" make THIS cost more ");
        Check("redeem rows: added at the end, existing reused", first == again && first.Title == "Make this cost more"
            && chatbot.Redeems.Rows.Count(r => r.Title != "") == 1 && chatbot.Redeems.Rows[^1].IsEmpty);
    }



    // ---- connection rows: which account only in the status box's tooltip ----
    private static void ConnectionRows()
    {
        var status = (Status: ConnectionStatus.Connected, Detail: "Connected to budderbot");
        var row = new StreamAssist.Components.Connections.UI.ConnectionRowViewModel("Bot", () => status, () => { }, () => { }, () => false, _ => { });
        Check("connection row: which account is in the tooltip", row.Detail == "Connected to budderbot" && row.IsConnected);
        status = (ConnectionStatus.Failed, "Could not log in");
        row.Refresh();
        Check("connection row: the tooltip follows the status", row.Detail == "Could not log in");
        Check("connection row: failed is red", row.IsDown);
        status = (ConnectionStatus.NotConnected, "");
        row.Refresh();
        Check("connection row: not connected is red too", row.IsDown);
        status = (ConnectionStatus.Connecting, "");
        row.Refresh();
        Check("connection row: connecting is grey", !row.IsDown);
    }



    // ---- Parse Parameter spacing ----
    private static void ParseParameterSpacing()
    {
        var m = Chatbot();
        var top = (MatchTextChatBlock)m.Root.Block!;
        var so = new ParameterChatBlock { ParameterName = "target" };
        var soBind = new BindableModel(); soBind.TryAddBlock(new ParamRecordBlock { Key = "target" }); so.Target.TryBind(soBind);
        top.Rows.AddRow("!so").Target.Child.TrySetBlock(so);
        var add = new ParameterChatBlock { ParameterName = "command" };
        var content = new ParameterChatBlock { ParameterName = "content", Reads = ParameterChatBlock.RestOfMessage };
        var cb = new BindableModel(); cb.TryAddBlock(new ParamRecordBlock { Key = "content" }); content.Target.TryBind(cb);
        add.Target.Child.TrySetBlock(content);
        top.Rows.AddRow("#addcom ").Target.Child.TrySetBlock(add);
        string Seen(string text) { ParamRecordBlock.Seen = null; m.TryMatchMessage(new ChatMatchState(text, 0, new Dictionary<string, string>())); return ParamRecordBlock.Seen ?? "(none)"; }
        Check("param: reads after a space", Seen("!so bob") == "bob");
        Check("param: won't read the end of a word", Seen("!sobob") == "(none)");
        Check("param: two in a row are split by the space", Seen("#addcom !hi hello there") == "hello there");
        Check("param: rest can be empty", Seen("#addcom !hi") == "");
        Check("param: bind box text has the spaces", m.Describe(so.Target)?.Text == "!so {target}" && m.Describe(content.Target)?.Text == "#addcom {command} {content}");
        Check("param: rest of message refuses a block after it", !content.Target.Child.Accepts(new MatchTextChatBlock()) && content.Target.EndsHere);
        Check("param: next word still takes one", add.Target.Child.Block != null && !add.Target.EndsHere);
        var switched = new ParameterChatBlock(); switched.Target.Child.TrySetBlock(new MatchTextChatBlock());
        switched.Reads = ParameterChatBlock.RestOfMessage;
        Check("param: switching to rest keeps a block already there", switched.Target.Child.Block != null && !switched.Target.Child.Accepts(new MatchTextChatBlock()));
        switched.Reads = ParameterChatBlock.NextWord;
        Check("param: switching back allows blocks again", !switched.Target.EndsHere);
        var reloaded = (ParameterChatBlock)BlockResolver.DeepCopy(content)!;
        Check("param: a loaded rest-of-message ends there too", reloaded.Target.EndsHere);
        Check("param: reads is saved", JsonSerializer.Serialize(m).Contains("\"reads\": \"Rest of message\"") || JsonSerializer.Serialize(m).Contains("\"reads\":\"Rest of message\""));
    }



    // ---- eventsub message parsing (examples shaped like the twitch docs) ----
    private static void EventSubParsing()
    {
        var welcome = EventSubMessage.Parse("""
            {"metadata":{"message_id":"96a3f3b5","message_type":"session_welcome","message_timestamp":"2023-07-19T14:56:51.634234626Z"},
             "payload":{"session":{"id":"AQoQILE98gtqShGmLD7AM6yJThAB","status":"connected","connected_at":"2023-07-19T14:56:51.616329898Z","keepalive_timeout_seconds":10,"reconnect_url":null}}}
            """);
        Check("welcome parsed", welcome.MessageType == "session_welcome" && welcome.SessionId == "AQoQILE98gtqShGmLD7AM6yJThAB" && welcome.KeepaliveSeconds == 10);

        var keepalive = EventSubMessage.Parse("""{"metadata":{"message_id":"84c1e79a","message_type":"session_keepalive","message_timestamp":"2023-07-19T10:11:12.634234626Z"},"payload":{}}""");
        Check("keepalive parsed", keepalive.MessageType == "session_keepalive" && keepalive.Event == null);

        var reconnect = EventSubMessage.Parse("""
            {"metadata":{"message_id":"84c1e79a","message_type":"session_reconnect","message_timestamp":"2022-11-18T09:10:11.634234626Z"},
             "payload":{"session":{"id":"AQoQexAWVYKSTIu4ec_2VAxyuhAB","status":"reconnecting","keepalive_timeout_seconds":null,"reconnect_url":"wss://eventsub.wss.twitch.tv?...","connected_at":"2022-11-16T10:11:12.634234626Z"}}}
            """);
        Check("reconnect parsed", reconnect.ReconnectUrl == "wss://eventsub.wss.twitch.tv?...");

        var revoke = EventSubMessage.Parse("""
            {"metadata":{"message_id":"84c1e79a","message_type":"revocation","message_timestamp":"2022-11-16T10:11:12.464757833Z","subscription_type":"channel.follow","subscription_version":"1"},
             "payload":{"subscription":{"id":"f1c2a387","status":"authorization_revoked","type":"channel.follow","version":"1","cost":1,"condition":{"broadcaster_user_id":"12826"},"transport":{"method":"websocket","session_id":"AQoQexAWVYKSTIu4ec_2VAxyuhAB"},"created_at":"2022-11-16T10:11:12.464757833Z"}}}
            """);
        Check("revocation parsed", revoke.RevokedType == "channel.follow" && revoke.RevokedReason == "authorization_revoked");

        TwitchEvent Notify(string type, string evJson) => EventSubMessage.Parse(
            "{\"metadata\":{\"message_id\":\"x\",\"message_type\":\"notification\",\"message_timestamp\":\"t\",\"subscription_type\":\"" + type + "\",\"subscription_version\":\"1\"},"
            + "\"payload\":{\"subscription\":{\"id\":\"s\",\"type\":\"" + type + "\"},\"event\":" + evJson + "}}").Event!;

        var chat = Notify("channel.chat.message", """{"broadcaster_user_id":"1971641","broadcaster_user_login":"streamer","broadcaster_user_name":"streamer","chatter_user_id":"4145994","chatter_user_login":"viewer32","chatter_user_name":"viewer32","message_id":"cc106a89","message":{"text":"!lurk bruh","fragments":[{"type":"text","text":"!lurk bruh"}]},"color":"#00FF7F","badges":[],"message_type":"text"}""");
        Check("chat message", chat.Type == TwitchEvent.ChatMessage && chat.GetString("chatter_user_name") == "viewer32" && chat.GetString("message", "text") == "!lurk bruh");

        var redeem = Notify("channel.channel_points_custom_reward_redemption.add", """{"id":"17fa2df1","broadcaster_user_id":"1337","user_name":"Cool_User","user_input":"pogchamp","status":"unfulfilled","reward":{"id":"92af127c","title":"title","cost":100,"prompt":"reward prompt"},"redeemed_at":"2020-07-15T17:16:03.17106713Z"}""");
        Check("redeem", redeem.GetString("reward", "title") == "title" && redeem.GetString("user_input") == "pogchamp");

        var sub = Notify("channel.subscribe", """{"user_id":"1234","user_login":"cool_user","user_name":"Cool_User","tier":"1000","is_gift":false}""");
        Check("sub", sub.GetString("tier") == "1000");
        var giftedSub = Notify("channel.subscribe", """{"user_name":"Cool_User","tier":"2000","is_gift":true}""");
        Check("gifted sub", giftedSub.GetString("is_gift") == "True");

        var gift = Notify("channel.subscription.gift", """{"user_id":null,"user_login":null,"user_name":null,"total":2,"tier":"1000","cumulative_total":null,"is_anonymous":true}""");
        Check("anonymous gift", gift.GetString("user_name") == null && gift.GetString("total") == "2");

        var raid = Notify("channel.raid", """{"from_broadcaster_user_name":"Twitch","to_broadcaster_user_name":"Twitch_Dev","viewers":9001}""");
        Check("raid", raid.GetString("viewers") == "9001");
    }



    // ---- chat history: the last messages, stepped through by id ----
    private static async Task ChatHistory()
    {
        var history = new StreamAssist.Components.Chatbot.Model.ChatHistoryModel();
        Dictionary<string, string> Msg(int n) => new() { ["messageId"] = $"m{n}", ["message"] = $"hi {n}", ["userLogin"] = $"user{n}" };
        Check("chat history: nothing yet is an error", !history.TryFind(StreamAssist.Components.Chatbot.Model.ChatHistoryStep.Newest, "", out _, out var hErr) && hErr == "no chat messages yet");
        for (var n = 1; n <= 3; n++) history.Add(Msg(n));
        string Step(StreamAssist.Components.Chatbot.Model.ChatHistoryStep step, string id) => history.TryFind(step, id, out var m, out _) ? m["messageId"] : "error";
        Check("chat history: newest, and an empty id counts as newest", Step(StreamAssist.Components.Chatbot.Model.ChatHistoryStep.Newest, "m1") == "m3"
            && Step(StreamAssist.Components.Chatbot.Model.ChatHistoryStep.Older, "") == "m3");
        Check("chat history: older / newer / this by id, staying put at the ends", Step(StreamAssist.Components.Chatbot.Model.ChatHistoryStep.Older, "m3") == "m2"
            && Step(StreamAssist.Components.Chatbot.Model.ChatHistoryStep.Older, "m1") == "m1" && Step(StreamAssist.Components.Chatbot.Model.ChatHistoryStep.Newer, "m3") == "m3"
            && Step(StreamAssist.Components.Chatbot.Model.ChatHistoryStep.This, "m2") == "m2");
        history.Add(Msg(4));
        Check("chat history: a new message doesn't move what you're on", Step(StreamAssist.Components.Chatbot.Model.ChatHistoryStep.Older, "m2") == "m1");
        for (var n = 5; n <= 104; n++) history.Add(Msg(n));
        Check("chat history: only the last 100 are kept, a dropped id is an error", history.Messages.Count == 100 && history.Messages[0]["messageId"] == "m5"
            && Step(StreamAssist.Components.Chatbot.Model.ChatHistoryStep.This, "m2") == "error");

        var chat = Chatbot();
        var oldChat = StreamAssist.Shell.GlobalContext.ChatbotComponentModel;
        StreamAssist.Shell.GlobalContext.ChatbotComponentModel = chat;
        chat.ChatHistory.Add(Msg(1));
        chat.ChatHistory.Add(Msg(2));
        var histBlock = new StreamAssist.Components.Chatbot.Values.ChatHistoryBlock().Pick("Find", "Older").Pick("Give", "userLogin");
        ((ValueSlot)histBlock.Slots[2]).SetText("m2");
        Check("chat history block: gives what it's asked for", await histBlock.EvaluateAsync(new TriggerContext(TriggerPhase.Once, default)) == "user1");
        StreamAssist.Shell.GlobalContext.ChatbotComponentModel = oldChat;
        chat.Dispose();
    }



    // ---- chatbot model + match text rows ----
    private static void ChatbotModelAndMatchRows()
    {
        var rows = new MatchTextChatBlock();
        Check("new match text has one empty row", rows.Rows.Rows.Count == 1 && rows.Rows.Rows[0].IsEmpty);
        rows.Rows.Rows[0].SetPattern("!");
        Check("typing adds a new empty row below", rows.Rows.Rows.Count == 2 && rows.Rows.Rows[1].IsEmpty);
        rows.Rows.Rows[1].SetPattern("x");
        rows.Rows.Rows[0].SetPattern("");
        Check("clearing a row with no block removes it", rows.Rows.Rows.Count == 2 && rows.Rows.Rows[0].Pattern == "x");
        rows.Rows.Rows[0].Target.Child.TrySetBlock(new MatchTextChatBlock());
        rows.Rows.Rows[0].SetPattern("");
        Check("clearing a row that has a block keeps it", rows.Rows.Rows.Count == 2 && rows.Rows.Rows[0].Target.Child.Block != null);
        rows.Rows.Rows[0].Target.Child.RemoveBlock(rows.Rows.Rows[0].Target.Child.Block!);
        Check("...and it goes once the block is removed too", rows.Rows.Rows.Count == 1 && rows.Rows.Rows[0].IsEmpty);
        Check("row slots only take chat blocks", !rows.Rows.Rows[0].Target.Child.TrySetBlock(new RecordBlock()));

        // binding OR block, never both
        var row0 = rows.Rows.Rows[0];
        Check("an empty row can be bound", row0.Target.TryBind(B("K")) && row0.Target.Bindable != null);
        Check("a bound row isn't removed even with no text", rows.Rows.Rows.Count == 2 && !row0.IsEmpty);
        Check("a bound row refuses a block", !row0.Target.Child.TrySetBlock(new MatchTextChatBlock()) && row0.Target.Child.Block == null);
        Check("a bound row refuses a second binding", !row0.Target.TryBind(B("K2")));
        var rowBinding = row0.Target.Bindable!;
        rowBinding.Root.RemoveBlock(rowBinding.Root.Blocks.FirstOrDefault()!);
        Check("removing the binding's blocks unbinds it (and the empty row goes)", row0.Target.Bindable == null && rows.Rows.Rows.Count == 1);
        var row1 = rows.Rows.Rows[0];
        row1.SetPattern("y");
        row1.Target.Child.TrySetBlock(new MatchTextChatBlock());
        Check("a row with a block refuses a binding", !row1.Target.TryBind(B("K3")) && row1.Target.Bindable == null);
        row1.Target.Child.RemoveBlock(row1.Target.Child.Block!);
        Check("...until the block is removed", row1.Target.TryBind(B("K4")));
        row1.Target.Unbind();
        Check("unbind clears it", row1.Target.Bindable == null && row1.Target.IsEmpty);

        // a file with both: binding kept, block dropped, warned (load only)
        var both = "{\"rows\":[{\"pattern\":\"z\",\"bindable\":{\"root\":[{\"type\":\"ChatLogBlock\",\"parameters\":{\"tag\":\"Z\"}}]},\"child\":{\"type\":\"MatchTextChatBlock\",\"parameters\":{\"rows\":[]}}}]}";
        var bothBlock = JsonSerializer.Deserialize<MatchTextChatBlock>(both)!;
        var bothTarget = bothBlock.Rows.Rows[0].Target;
        Check("a saved row with both keeps the binding and drops the block", bothTarget.Bindable != null && bothTarget.Child.Block == null);

        var model = Chatbot();
        int chatChanges = 0; model.Changed += () => chatChanges++;
        var lurkBlock = M(("lurk", B("L")));
        var tree = M(("!", lurkBlock));
        Check("fresh chatbot root starts with a Match Text", model.Root.Block is MatchTextChatBlock);
        model.Root.TrySetBlock(tree);
        chatChanges = 0;
        lurkBlock.Rows.Rows[0].SetPattern("lurk ");
        Check("typing in a nested row bubbles to the model", chatChanges >= 1);
        Check("block contains its row slots (no self-drop)", tree.ContainsSlot(lurkBlock.Rows.Rows[0].Target.Child));

        var chatJson = JsonSerializer.Serialize(model);
        var loaded = JsonSerializer.Deserialize<ChatbotComponentModel>(chatJson)!;
        Check("chatbot model round trip", JsonSerializer.Serialize(loaded) == chatJson);
        Check("trailing empty row not saved but restored", !chatJson.Contains("\"pattern\":\"\"") && ((MatchTextChatBlock)loaded.Root.Block!).Rows.Rows[^1].IsEmpty);
        Log(chatJson);
    }



    // ---- chat matching ----
    private static async Task ChatMatching()
    {
        var model = Chatbot();
        var disabled = B("OFF"); disabled.SetIsDisabled(true);
        var tree = M(("!", M(
            ("lurk", B("LURK")),
            ("pay", P("user", ParameterChatBlock.NextWord, P("amount", ParameterChatBlock.NextWord, B("PAY")))),
            ("/give (?<amount>\\d+)/", B("GIVE")),
            ("so", P("target", ParameterChatBlock.RestOfMessage, B("SO"))),
            ("first", B("FIRST")),
            ("first", B("SECOND")),
            ("/x/", M(("/y/", B("XY")))),
            ("/x/", B("X")),
            ("off", disabled),
            ("/(bad/", B("BAD")))));
        model.Root.TrySetBlock(tree);

        async Task<string> Chat(string text)
        {
            ChatLogBlock.Log.Clear();
            ChatBlock.TryMatchSlot(model.Root, new ChatMatchState(text, 0, new Dictionary<string, string> { ["user"] = "viewer" }));
            await Task.Delay(50);
            return string.Join(";", ChatLogBlock.Log);
        }

        Check("!lurk", await Chat("!lurk") == "LURK|rest=|user=viewer|amount=|target=");
        Check("!lurk bruh (rest)", await Chat("!lurk bruh") == "LURK|rest= bruh|user=viewer|amount=|target=");
        Check("!lurkbruh doesn't match", await Chat("!lurkbruh") == "");
        Check("case is ignored", await Chat("!LURK") == "LURK|rest=|user=viewer|amount=|target=");
        Check("(?-i) still makes a row case sensitive", ((Func<bool>)(() => { var m = M(("/(?-i)Exact/", B("E"))); return !m.TryMatch(new ChatMatchState("exact", 0, new Dictionary<string, string>())); }))());
        Check("parameters (one word + digits)", await Chat("!pay joe 50") == "PAY|rest=|user=joe|amount=50|target=");
        Check("next word needs a word", await Chat("!pay joe") == "" && await Chat("!pay joe   ") == "");
        Check("next word skips extra spaces", await Chat("!pay   joe 7") == "PAY|rest=|user=joe|amount=7|target=");
        Check("rest of message is trimmed", await Chat("!so   cool streamer  ") == "SO|rest=|user=viewer|amount=|target=cool streamer");
        Check("named group becomes parameter", await Chat("!give 25 now") == "GIVE|rest= now|user=viewer|amount=25|target=");
        Check("rest-of-message parameter", await Chat("!so cool streamer") == "SO|rest=|user=viewer|amount=|target=cool streamer");
        Check("only first match runs", await Chat("!first") == "FIRST|rest=|user=viewer|amount=|target=");
        Check("backtracks to next row", await Chat("!xz") == "X|rest=z|user=viewer|amount=|target=");
        Check("deeper match preferred in order", await Chat("!xy") == "XY|rest=|user=viewer|amount=|target=");
        Check("disabled block skipped", await Chat("!off") == "");
        Check("invalid pattern doesn't crash", await Chat("!(bad") == "");
        Check("anchored at position (no match mid-message)", await Chat("hi !lurk") == "");

        var innerMatch = (MatchTextChatBlock)tree.Rows.Rows[0].Target.Child.Block!;
        var payParam = (ParameterChatBlock)innerMatch.Rows.Rows[1].Target.Child.Block!;
        var lurkBind = innerMatch.Rows.Rows[0].Target.Bindable!;
        var flashed = new List<string>();
        tree.Triggered += () => flashed.Add("outer"); innerMatch.Triggered += () => flashed.Add("innerMatch");
        payParam.Triggered += () => flashed.Add("pay"); lurkBind.Triggered += () => flashed.Add("lurkBindable");
        await Chat("!lurk");
        Check("everything that matched flashes", flashed.OrderBy(x => x).SequenceEqual(new[] { "innerMatch", "lurkBindable", "outer" }));
        flashed.Clear(); await Chat("!pay joe nope");
        Check("partial match flashes up to where it stopped", flashed.OrderBy(x => x).SequenceEqual(new[] { "innerMatch", "outer", "pay" }));
        flashed.Clear(); await Chat("!bruh");
        Check("only the ! block flashes for !bruh", flashed.SequenceEqual(new[] { "outer" }));
        flashed.Clear(); await Chat("hello");
        Check("nothing flashes when nothing matches", flashed.Count == 0);

        tree.SetIsCollapsed(true);
        var collapsedJson = JsonSerializer.Serialize(model);
        Check("collapsed state is saved", JsonSerializer.Deserialize<ChatbotComponentModel>(collapsedJson)!.Root.Block!.IsCollapsed);
        tree.SetIsCollapsed(false);

        Check("a parameter has one target (binding or block)", payParam.Slots.Count == 1 && payParam.Slots[0] is ChatTarget);
        var amountParam = (ParameterChatBlock)payParam.Target.Child.Block!;
        Check("command text for a nested target", ChatbotComponentModel.FormatCommand(model.FindCommandSteps(amountParam.Target)!) == "!pay {user} {amount}");
        Check("command text for a row", ChatbotComponentModel.FormatCommand(model.FindCommandSteps(innerMatch.Rows.Rows[0].Target)!) == "!lurk");
        Check("a target outside the tree has no command", model.FindCommandSteps(new ChatTarget()) == null);

        var matchJson = JsonSerializer.Serialize(model);
        var loaded = JsonSerializer.Deserialize<ChatbotComponentModel>(matchJson)!;
        model.Root.TrySetBlock(BlockResolver.DeepCopy(loaded.Root.Block!)!);
        Check("matching works after save/load", await Chat("!pay joe 7") == "PAY|rest=|user=joe|amount=7|target=");
        Check("credentials never saved in chatbot.json", !matchJson.Contains("clientId") && !matchJson.Contains("clientSecret"));

        // parameters reach the blocks
        var pb = new BindableModel(); pb.TryAddBlock(new ParamRecordBlock { Key = "user" });
        ParamRecordBlock.Seen = null;
        pb.TriggerOnce(new Dictionary<string, string> { ["user"] = "joe" });
        await Task.Delay(50);
        Check("TriggerOnce passes parameters", ParamRecordBlock.Seen == "joe");

        // built-in chat parameters + formatting
        var chatEvent = EventSubMessage.Parse(
            "{\"metadata\":{\"message_id\":\"x\",\"message_type\":\"notification\",\"message_timestamp\":\"2026-10-28T18:30:00Z\",\"subscription_type\":\"channel.chat.message\",\"subscription_version\":\"1\"},"
            + "\"payload\":{\"subscription\":{\"id\":\"s\"},\"event\":{\"broadcaster_user_id\":\"1\",\"broadcaster_user_login\":\"streamer\",\"broadcaster_user_name\":\"Streamer\",\"chatter_user_id\":\"2\",\"chatter_user_login\":\"viewer32\",\"chatter_user_name\":\"Viewer32\",\"message_id\":\"m1\",\"message\":{\"text\":\"!lurk\"},\"color\":\"#00FF7F\",\"badges\":[{\"set_id\":\"moderator\",\"id\":\"1\",\"info\":\"\"},{\"set_id\":\"subscriber\",\"id\":\"12\",\"info\":\"16\"}]}}}").Event!;
        var p = chatEvent.ChatParameters();
        Check("chat parameters", p["user"] == "Viewer32" && p["userLogin"] == "viewer32" && p["message"] == "!lurk" && p["channel"] == "Streamer"
            && p["isModerator"] == "true" && p["isSubscriber"] == "true" && p["isVip"] == "false" && p["badges"] == "moderator,subscriber");
        var ctx = new TriggerContext(TriggerPhase.Once, default, p);
        Check("format placeholders", ctx.Format("{user} is lurking in {channel} {unknown}") == "Viewer32 is lurking in Streamer {unknown}");
    }



    // ---- redeems + stream events ----
    private static async Task RedeemsAndStreamEvents()
    {
        TwitchEvent Ev(string type, string evJson) => EventSubMessage.Parse(
            "{\"metadata\":{\"message_id\":\"x\",\"message_type\":\"notification\",\"message_timestamp\":\"t\",\"subscription_type\":\"" + type + "\",\"subscription_version\":\"1\"},"
            + "\"payload\":{\"subscription\":{\"id\":\"s\",\"type\":\"" + type + "\"},\"event\":" + evJson + "}}").Event!;
        BindableModel Rec(string key) { var b = new BindableModel(); b.TryAddBlock(new ParamRecordBlock { Key = key }); return b; }
        async Task<string?> Seen(Action run) { ParamRecordBlock.Seen = null; run(); await Task.Delay(50); return ParamRecordBlock.Seen; }

        var model = Chatbot();
        Check("fresh redeem list has one empty row", model.Redeems.Rows.Count == 1 && model.Redeems.Rows[0].IsEmpty);
        model.Redeems.Rows[0].SetTitle("Hydrate!");
        Check("typing a title adds a new empty row", model.Redeems.Rows.Count == 2);
        var hydrate = model.Redeems.Rows[0];
        Check("redeem rows only run bindings", !hydrate.Target.Child.TrySetBlock(new MatchTextChatBlock()) && !hydrate.Target.AllowsBlocks);
        hydrate.Target.TryBind(Rec("input"));

        var input = await Seen(() => model.Redeems.TryRun("  hydrate!", new Dictionary<string, string> { ["input"] = "water" }));
        Check("redeem title matches exactly, ignoring case and spaces", input == "water");
        Check("other titles don't match", !model.Redeems.TryRun("Hydrate", new Dictionary<string, string>()));
        Check("no regex in titles", !model.Redeems.TryRun("HydrateX", new Dictionary<string, string>()));
        hydrate.Target.Bindable!.SetIsDisabled(true);
        Check("disabled redeem doesn't run", !model.Redeems.TryRun("hydrate!", new Dictionary<string, string>()));
        hydrate.Target.Bindable!.SetIsDisabled(false);
        hydrate.SetTitle("");
        Check("a bound redeem with no title is kept", model.Redeems.Rows.Count == 2 && model.Redeems.Rows[0] == hydrate);
        hydrate.SetTitle("Hydrate!");

        Check("all event slots exist", model.Events.Select(e => e.Name).SequenceEqual(new[] { "Follow", "Sub", "Gift Subs", "Cheer", "Raid", "Stream Online", "Stream Offline", "App Started" }));
        var raidSlot = model.Events.First(e => e.Key == ChatEvents.Raid);
        Check("event slots only run bindings", !raidSlot.Target.Child.TrySetBlock(new MatchTextChatBlock()));
        Check("unbound event does nothing", !model.TryRunEvent(ChatEvents.Raid, new Dictionary<string, string>()));
        raidSlot.Target.TryBind(Rec("viewers"));
        model.Events.First(e => e.Key == ChatEvents.Sub).Target.TryBind(Rec("months"));
        model.Events.First(e => e.Key == ChatEvents.GiftSubs).Target.TryBind(Rec("count"));
        model.Events.First(e => e.Key == ChatEvents.Cheer).Target.TryBind(Rec("user"));

        Check("describe redeem", model.Describe(hydrate.Target) == ("Redeem", "Hydrate!"));
        Check("describe event", model.Describe(raidSlot.Target) == ("Alert", "Raid"));

        // twitch events -> parameters -> bindings
        Check("raid from twitch", await Seen(() => ChatEventManager.Dispatch(model, Ev("channel.raid", """{"from_broadcaster_user_name":"Twitch","from_broadcaster_user_login":"twitch","from_broadcaster_user_id":"1","to_broadcaster_user_name":"Me","viewers":9001}"""))) == "9001");
        Check("redeem from twitch", await Seen(() => ChatEventManager.Dispatch(model, Ev("channel.channel_points_custom_reward_redemption.add", """{"user_name":"Cool_User","user_input":"pogchamp","reward":{"id":"r","title":"HYDRATE!","cost":100}}"""))) == "pogchamp");
        Check("new sub is month 1", await Seen(() => ChatEventManager.Dispatch(model, Ev("channel.subscribe", """{"user_name":"Viewer","tier":"1000","is_gift":false}"""))) == "1");
        Check("gifted sub is left to Gift Subs", await Seen(() => ChatEventManager.Dispatch(model, Ev("channel.subscribe", """{"user_name":"Viewer","tier":"1000","is_gift":true}"""))) == null);
        Check("resub goes to Sub with months", await Seen(() => ChatEventManager.Dispatch(model, Ev("channel.subscription.message", """{"user_name":"Viewer","tier":"2000","cumulative_months":15,"streak_months":null,"message":{"text":"hi"}}"""))) == "15");
        Check("gift subs count", await Seen(() => ChatEventManager.Dispatch(model, Ev("channel.subscription.gift", """{"user_name":null,"total":5,"tier":"1000","is_anonymous":true}"""))) == "5");
        Check("anonymous cheer", await Seen(() => ChatEventManager.Dispatch(model, Ev("channel.cheer", """{"is_anonymous":true,"user_name":null,"bits":100,"message":"cheer100"}"""))) == "Anonymous");
        Check("chat messages aren't events", await Seen(() => ChatEventManager.Dispatch(model, Ev("channel.chat.message", """{"chatter_user_name":"x","message":{"text":"hi"}}"""))) == null);

        // test values use the same parameter names
        var sample = ChatEvents.SampleParameters(ChatEvents.Raid, "TestUser", "42", "1", "");
        Check("sample raid parameters", sample["viewers"] == "42" && sample["user"] == "TestUser" && sample.ContainsKey("channel"));

        // saving
        var eventsJson = JsonSerializer.Serialize(model);
        var loadedEvents = JsonSerializer.Deserialize<ChatbotComponentModel>(eventsJson)!;
        Check("redeems + events round trip", JsonSerializer.Serialize(loadedEvents) == eventsJson);
        Check("unbound events aren't saved", eventsJson.Contains("\"raid\"") && !eventsJson.Contains("\"follow\""));
        Check("loaded redeem still runs", await Seen(() => loadedEvents.Redeems.TryRun("hydrate!", new Dictionary<string, string> { ["input"] = "tea" })) == "tea");
        var unknown = JsonSerializer.Deserialize<ChatbotComponentModel>("{\"events\":{\"nope\":{\"root\":[{\"type\":\"ParamRecordBlock\",\"parameters\":{\"key\":\"x\"}}]}}}")!;
        Check("unknown saved event is skipped", unknown.Events.All(e => e.Target.Bindable == null));

        int eventChanges = 0; model.Changed += () => eventChanges++;
        hydrate.SetTitle("Hydrate");
        raidSlot.Target.Bindable!.SetIsDisabled(true);
        Check("redeem + event changes reach the model (saving)", eventChanges >= 2);
    }



    // ---- connection settings ----
    private static void ConnectionSettings()
    {
        var startup = JsonSerializer.Deserialize<ChatbotComponentModel>("{\"connectOnStartup\":true,\"botConnectOnStartup\":false}")!;
        Check("connect on startup sets this session's state on load", startup.IsEnabled && startup.ConnectOnStartup && !startup.IsBotEnabled);

        int saves = 0, reconnects = 0, botReconnects = 0;
        startup.Changed += () => saves++;
        startup.ConnectionChanged += () => reconnects++;
        startup.BotConnectionChanged += () => botReconnects++;

        startup.SetIsEnabled(false);
        Check("disconnecting isn't saved and keeps the setting", saves == 0 && reconnects == 1 && startup.ConnectOnStartup);
        startup.Reconnect();
        Check("retry/connect turns it back on", startup.IsEnabled && reconnects == 2);
        startup.Reconnect();
        Check("retry while on asks for a reconnect", reconnects == 3);
        startup.ReconnectBot();
        Check("bot connect", startup.IsBotEnabled && botReconnects == 1);
        startup.SetBotConnectOnStartup(true);
        Check("connect on startup is saved", saves == 1 && JsonSerializer.Serialize(startup).Contains("\"botConnectOnStartup\":true"));
        Check("session state isn't saved", !JsonSerializer.Serialize(startup).Contains("isEnabled"));
        Check("chat only as bot: off by default", !startup.ChatOnlyAsBot && !Chatbot().ChatOnlyAsBot);
        startup.SetChatOnlyAsBot(true);
        startup.SetChatOnlyAsBot(true);
        Check("chat only as bot: saved once, without reconnecting", saves == 2 && reconnects == 3 && botReconnects == 1
            && JsonSerializer.Deserialize<ChatbotComponentModel>(JsonSerializer.Serialize(startup))!.ChatOnlyAsBot);
        var botRow = new StreamAssist.Components.Connections.UI.ConnectionRowViewModel("Bot", () => (ConnectionStatus.NotConnected, ""), () => { }, () => { },
            () => startup.BotConnectOnStartup, startup.SetBotConnectOnStartup, "Chat only as bot", () => startup.ChatOnlyAsBot, startup.SetChatOnlyAsBot);
        Check("chat only as bot: the bot row shows and changes it", botRow.HasOption && botRow.Option && botRow.OptionLabel == "Chat only as bot"
            && new Func<bool>(() => { botRow.Option = false; return !startup.ChatOnlyAsBot; })());
        Check("chat only as bot: other rows have no extra checkbox", !new StreamAssist.Components.Connections.UI.ConnectionRowViewModel("OBS", () => (ConnectionStatus.NotConnected, ""), () => { }, () => { }, () => false, _ => { }).HasOption);

        var audio = JsonSerializer.Deserialize<StreamAssist.Components.Audio.Model.AudioComponentModel>("{\"voicemeeterConnectOnStartup\":true}")!;
        int vmChanges = 0; audio.VoicemeeterConnectionChanged += () => vmChanges++;
        Check("voicemeeter connect on startup", audio.IsVoicemeeterEnabled);
        audio.SetIsVoicemeeterEnabled(false);
        audio.ReconnectVoicemeeter();
        audio.ReconnectVoicemeeter();
        Check("voicemeeter disconnect / connect / retry", audio.IsVoicemeeterEnabled && vmChanges == 3 && audio.VoicemeeterConnectOnStartup);
    }



    // ---- twitch action queue + moderation blocks ----
    private static async Task ActionQueueAndModeration()
    {
        // one at a time, in order, spaced by the interval
        var queue = new TwitchActionQueue(TimeSpan.FromMilliseconds(200));
        var started = new List<(int Id, DateTime At)>();
        var queued = Enumerable.Range(1, 4).Select(id => queue.RunAsync(async () =>
        {
            lock (started) started.Add((id, DateTime.UtcNow));
            await Task.Delay(10);
            return id;
        }, CancellationToken.None)).ToList();
        var results = await Task.WhenAll(queued);
        Check("queue runs every action, in order", results.SequenceEqual(new[] { 1, 2, 3, 4 }) && started.Select(s => s.Id).SequenceEqual(new[] { 1, 2, 3, 4 }));
        var gaps = started.Zip(started.Skip(1), (a, b) => (b.At - a.At).TotalMilliseconds).ToList();
        Check("queue spaces them by the interval", gaps.All(g => g >= 190));

        // a cancelled action gives up its place, the rest still run
        var slowQueue = new TwitchActionQueue(TimeSpan.FromMilliseconds(300));
        var first = slowQueue.RunAsync(() => Task.FromResult(1), CancellationToken.None);
        using var cancel = new CancellationTokenSource();
        var cancelled = slowQueue.RunAsync(() => Task.FromResult(2), cancel.Token);
        var third = slowQueue.RunAsync(() => Task.FromResult(3), CancellationToken.None);
        cancel.Cancel();
        var cancelledThrew = false;
        try { await cancelled; } catch (OperationCanceledException) { cancelledThrew = true; }
        Check("a cancelled action is dropped, the next still runs", await first == 1 && cancelledThrew && await third == 3);

        // blocks: start with useful parameters, save, and fail cleanly with no chatbot
        var timeout = new TimeOutUserAction();
        Check("Time Out User defaults to whoever triggered it, 60s",
            ((ValueSlot)timeout.Slots[0]).Text == "{userLogin}" && ((ValueSlot)timeout.Slots[1]).Text == "60" && ((ValueSlot)timeout.Slots[2]).Text == "");
        Check("Ban User defaults to whoever triggered it", ((ValueSlot)new BanUserAction().Slots[0]).Text == "{userLogin}");
        Check("Delete Chat Message defaults to the triggering message", ((ValueSlot)new DeleteChatMessageAction().Slots[0]).Text == "{messageId}");
        ((ValueSlot)timeout.Slots[1]).SetText("{seconds}");
        var timeoutJson = JsonSerializer.Serialize(BlockResolver.GetData(timeout));
        var timeoutLoaded = BlockResolver.Resolve(JsonSerializer.Deserialize<PendingBlockData>(timeoutJson)!)!;
        Check("Time Out User round trip (a saved value isn't replaced by the default)",
            JsonSerializer.Serialize(BlockResolver.GetData(timeoutLoaded)) == timeoutJson && ((ValueSlot)timeoutLoaded.Slots[1]).Text == "{seconds}");
        Check("moderation blocks are actions, under Twitch", new TimeOutUserAction() is ActionBlock && new TimeOutUserAction().Group == BlockGroup.Twitch && new BanUserAction().Group == BlockGroup.Twitch
            && new DeleteChatMessageAction().Group == BlockGroup.Twitch && BlockResolver.DiscoverAll().Any(b => b is SendChatMessageAction));
    }



    // ---- duplicate chat messages ----
    private static void DuplicateChatMessages()
    {
        var cgj = " " + (char)0x034F;
        var first = ChatbotManager.AvoidDuplicate("yo", null);
        var second = ChatbotManager.AvoidDuplicate("yo", first);
        var third = ChatbotManager.AvoidDuplicate("yo", second);
        Check("repeats alternate: yo, yo + space + CGJ, yo", first == "yo" && second == "yo" + cgj && third == "yo");
        Check("a different message is left alone", ChatbotManager.AvoidDuplicate("hi", "yo") == "hi");
        Check("spaces around don't make it different", ChatbotManager.AvoidDuplicate("yo ", "yo") == "yo " + cgj);
    }
}
