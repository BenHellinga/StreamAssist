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
using StreamAssist.Components.Keyboard.Conditions;
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
using StreamAssist.Components.Keyboard.Background;
using StreamAssist.Components.Keyboard.Model;
using StreamAssist.Components.Keyboard.UI;
using System.Text.Json;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Blocks.Containers;
using StreamAssist.Components.Blocks.UI;

namespace StreamAssist.Tests;



// every engine check, run in order (later sections reuse some helpers and state from earlier ones), returns the names of
// the checks that failed. The first sections count console errors, which AppLog posts through Avalonia's dispatcher, so
// they run before anything awaits, while this thread still owns it
internal static class EngineChecks
{
    public static async Task<List<string>> RunAsync(Action<string> log)
    {
        var failed = new List<string>();
        int fails = 0;
        void Check(string name, bool ok) { log($"{(ok ? "PASS" : "FAIL")}  {name}"); if (!ok) { fails++; failed.Add(name); } }
        ChatbotComponentModel Chatbot() { var c = new ChatbotComponentModel(); c.ChatConfigs.TryCreate("Default"); c.EventsConfigs.TryCreate("Default"); return c; }
        KeyboardListModel Keyboards() { var k = new KeyboardListModel(); k.Configs.TryCreate("Default"); return k; }
        ConvertBlock IntParam(string name) { var c = new ConvertBlock { To = "Int" }; ((ValueSlot)c.Slots[0]).SetText($"{{{name}}}"); return c; }
        ConvertBlock DoubleParam(string name) { var c = new ConvertBlock { To = "Double" }; ((ValueSlot)c.Slots[0]).SetText($"{{{name}}}"); return c; }
        ConvertBlock MakeConvert(string to, string text) { var c = new ConvertBlock { To = to }; ((ValueSlot)c.Slots[0]).SetText(text); return c; }

        // ---- value errors (first, while the main thread still owns the dispatcher that AppLog posts to) ----
        {
            var ctx = new TriggerContext(TriggerPhase.Once, default, new Dictionary<string, string> { ["user"] = "Viewer", ["bad"] = "lots" });
            var noParams = new TriggerContext(TriggerPhase.Start, default);
            T Slot<T>(BlockModel b, int i) where T : BlockSlotBase => (T)b.Slots[i];
            int Errors() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Count(l => l.Contains("] error - ")); }
            string LastError() => AppLog.Lines.Last(l => l.Contains("] error - "));

            // these blocks never really wait, so the run finishes synchronously on this thread
            (int Errors, List<string> Ran) RunConsole(Action<ConsoleMessageAction> setup, TriggerContext context)
            {
                var before = Errors();
                RecordBlock.Log.Clear();
                var list = new ListBlock();
                var console = new ConsoleMessageAction(); setup(console);
                Slot<BlockSlotList>(list, 0).TryInsertBlock(console, 0);
                Slot<BlockSlotList>(list, 0).TryInsertBlock(new RecordBlock { Tag = "after" }, 1);
                list.SafeRunAsync(context).GetAwaiter().GetResult();
                return (Errors() - before, RecordBlock.Log.ToList());
            }

            var missing = RunConsole(c => Slot<ValueSlot>(c, 0).BlockSlot.TrySetBlock(IntParam("user")), noParams);
            Check("missing parameter: one error, run stopped", missing.Errors == 1 && missing.Ran.Count == 0);
            Check("the error names the block", LastError().Contains("error - Convert: '{user}'"));

            var notANumber = RunConsole(c => Slot<ValueSlot>(c, 0).BlockSlot.TrySetBlock(IntParam("bad")), ctx);
            Check("bad int parameter: one error", notANumber.Errors == 1 && LastError().Contains("'lots' is not a whole number"));

            var typedBad = RunConsole(c => { var r = new RandomNumberBlock(); Slot<ValueSlot>(r, 0).SetText("abc"); Slot<ValueSlot>(c, 0).BlockSlot.TrySetBlock(r); }, ctx);
            Check("typed value that isn't a number: one error naming block + slot", typedBad.Errors == 1 && LastError().Contains("error - Random Number: Min 'abc' is not"));

            var crash = RunConsole(c => Slot<ValueSlot>(c, 0).SetText("fine"), ctx);
            Check("a working run logs no error and carries on", crash.Errors == 0 && crash.Ran.SequenceEqual(new[] { "after" }));
        }


        // ---- editable commands, two roots, Has Permission (on the main thread, errors are counted) ----
        {
            int Errors() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Count(l => l.Contains("] error - ")); }
            string LastError() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Last(l => l.Contains("] error - ")); }
            BindableModel Bind(BlockModel root) { var b = new BindableModel(); b.Root.TrySetBlock(root); return b; }
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
            Check("add: whole command", model.TryAddCommand("!hello", "  hi there ") && Cmd("!hello")?.Target.Bindable?.Root.Block is SendChatMessageAction { Message.Text: "hi there" });
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
            Check("edit: send-only command", model.TryEditCommand("!hello", "new text") && ((SendChatMessageAction)Cmd("!hello")!.Target.Bindable!.Root.Block!).Message.Text == "new text");
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
        {
            var m = Chatbot();
            var editable = (MatchTextChatBlock)m.EditableRoot.Block!;
            var bangMenu = new MatchTextChatBlock();
            editable.Rows.AddRow("!").Target.Child.TrySetBlock(bangMenu);
            var lurk = new SendChatMessageAction(); lurk.Message.SetText("LURK LURK LURK!!!");
            var lb = new BindableModel(); lb.Root.TrySetBlock(lurk); bangMenu.Rows.AddRow("lurk").Target.TryBind(lb);
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
            var fancy = new BindableModel(); fancy.Root.TrySetBlock(new ListBlock()); bangMenu.Rows.AddRow("fancy").Target.TryBind(fancy);
            Check("failure: edit a command that does more is NotSimple", F(() => { m.TryEditCommand("!fancy", "z", out var f, logErrors: false); return f; }) == CommandFailure.NotSimple);
            Check("failure: no name is Invalid", F(() => { m.TryAddCommand(" ", "z", out var f, logErrors: false); return f; }) == CommandFailure.Invalid);
            Check("failure: success is None", F(() => { m.TryEditCommand("!lurk", "ok", out var f); return f; }) == CommandFailure.None);
            var editableJson = JsonSerializer.Serialize(m);
            var m2 = JsonSerializer.Deserialize<ChatbotComponentModel>(editableJson)!;
            Check("nested: works after save/load", m2.TryEditCommand("!lurk", "again") && m2.TryDeleteCommand("!rec"));
        }

        // ---- plain text rows vs /regex/ rows ----
        {
            var m = Chatbot();
            var top = (MatchTextChatBlock)m.Root.Block!;
            var bang = new MatchTextChatBlock();
            top.Rows.AddRow("!").Target.Child.TrySetBlock(bang);
            BindableModel Rec(string tag) { var b = new BindableModel(); b.Root.TrySetBlock(new RecordBlock { Tag = tag }); return b; }
            BindableModel Seen(string key) { var b = new BindableModel(); b.Root.TrySetBlock(new ParamRecordBlock { Key = key }); return b; }
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
            var get = new GetRewardSettingBlock { Setting = "Title" };
            Check("get reward setting: picking a setting picks its type", get.Kind == ValueKind.Text);
            get.Setting = "Paused";
            Check("get reward setting: bool setting gives true/false", get.Kind == ValueKind.Bool);
            var getLoaded = (GetRewardSettingBlock)BlockResolver.DeepCopy(new GetRewardSettingBlock { Setting = "Cost", As = "Text" })!;
            Check("get reward setting: a saved type is kept", getLoaded.Setting == "Cost" && getLoaded.Kind == ValueKind.Text);
            var set = new SetRewardSettingAction();
            ((ValueSlot)set.Slots[1]).BlockSlot.TrySetBlock(new ExpressionBlock());
            ((ExpressionBlock)((ValueSlot)set.Slots[1]).BlockSlot.Block!).Formula.SetText("{cost}+1");
            var setList = new ListBlock();
            ((BlockSlotList)setList.Slots[0]).TryInsertBlock(set, 0);
            ((BlockSlotList)setList.Slots[0]).TryInsertBlock(new RecordBlock { Tag = "after" }, 1);
            RecordBlock.Log.Clear();
            var e2 = Errors();
            setList.SafeRunAsync(redeem).GetAwaiter().GetResult();
            Check("set reward setting: no chatbot fails with one error and stops", Errors() == e2 + 1 && RecordBlock.Log.Count == 0);
            Check("set reward setting: starts on the reward just redeemed", ((ValueSlot)new SetRewardSettingAction().Slots[0]).Text == "{reward}");

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

        // ---- Expression: typed formulas (main thread, errors are counted) ----
        {
            int Errors() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Count(l => l.Contains("] error - ")); }
            string LastError() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Last(l => l.Contains("] error - ")); }
            (double Value, bool Whole)? F(string text) => StreamAssist.Core.Utilities.Formula.TryEvaluate(text, out var r, out _) ? (r.Value, r.IsWhole) : null;
            string Err(string text) { StreamAssist.Core.Utilities.Formula.TryEvaluate(text, out _, out var e); return e; }

            Check("formula: precedence and parentheses", F("2+3*4") == (14, true) && F("(2+3)*4") == (20, true) && F("10-3-2") == (5, true));
            Check("formula: triangle numbers", F("25*(25+1)/2") == (325, true) && F("(25-1)*25/2") == (300, true));
            Check("formula: whole division drops decimals like C#", F("7/2") == (3, true) && F("-7/2") == (-3, true) && F("7%3") == (1, true));
            Check("formula: a decimal makes that part decimal", F("7/2.0") == (3.5, false) && F("7/2*2.0") == (6, false) && F("1.5+1") == (2.5, false));
            Check("formula: signs, spaces and × ÷ −", F(" -3 + -(-2) ") == (-1, true) && F("6 × 7") == (42, true) && F("9 ÷ 3 − 1") == (2, true) && F("3--5") == (8, true));
            Check("formula: mistakes are explained", Err("1/0").Contains("divide by zero") && Err("(1+2").Contains("never closed") && Err("2+").Contains("ends too early") && Err("2 x 3").Contains("didn't expect 'x'") && Err("1.2.3").Contains("isn't a number"));

            var ctx = new TriggerContext(TriggerPhase.Once, default, new Dictionary<string, string> { ["cost"] = "25" });
            ExpressionBlock Expr(string text) { var e = new ExpressionBlock(); e.Formula.SetText(text); return e; }
            object Eval(ValueBlock block) => block.EvaluateObjectAsync(ctx).GetAwaiter().GetResult();
            Check("expression: parameters are filled in first", Eval(Expr("{cost}*({cost}+1)/2")) is 325.0);
            var intSlot = new ValueSlot<int>("x");
            var placed = Expr("{cost}+1");
            Check("expression: in a whole number slot it gives a whole number", intSlot.BlockSlot.TrySetBlock(placed) && placed.Kind == ValueKind.Int && Eval(placed) is 26);
            var copied = (ExpressionBlock)BlockResolver.DeepCopy(placed)!;
            Check("expression: saved and loaded keeps its formula and type", copied.Kind == ValueKind.Int && Eval(copied) is 26);
            var e0 = Errors();
            var exprFailed = false;
            try { Eval(Expr("{cost}/0")); } catch (BlockFailedException) { exprFailed = true; }
            Check("expression: a bad formula fails with one error naming it", exprFailed && Errors() == e0 + 1 && LastError().Contains("error - Expression: '25/0' can't divide by zero"));
            var builder = new StringBuilderBlock();
            builder.Text.SetText("total: {t}");
            var tRow = builder.Values.Items[^1];
            tRow.Key.SetText("t");
            tRow.Value.BlockSlot.TrySetBlock(Expr("{cost}*({cost}+1)/2"));
            Check("expression: a whole answer reads cleanly as text", Eval(builder) is "total: 325");

            // true/false: comparisons and logic, like C#
            bool? Bf(string text) => StreamAssist.Core.Utilities.Formula.TryEvaluate(text, out var r, out _) && r.IsBool ? r.AsBool : null;
            Check("formula: comparisons", Bf("3 == 3.0") == true && Bf("3 != 3") == false && Bf("2 < 3") == true && Bf("3 <= 2") == false && Bf("4 >= 4") == true && Bf("5 ≥ 6") == false);
            Check("formula: && || ! and true / false, C#'s precedence", Bf("1 < 2 && 2 < 3") == true && Bf("false || 1 == 1") == true && Bf("!(1 == 1)") == false
                && Bf("true && !false") == true && Bf("1 + 1 == 2 || false && false") == true && Bf("TRUE") == true);
            Check("formula: maths and true/false don't mix", Err("true + 1").Contains("needs numbers") && Err("5 && 1").Contains("needs true/false")
                && Err("5 == true").Contains("can't compare") && Err("1 = 1") != "" && Bf("5") == null);
            var ifCondition = new BlockBranchList("");
            ifCondition.TryInsertBranch(Expr("{cost} == 25 && !({cost} > 30)"), 0);
            var asCondition = (ExpressionBlock)ifCondition.Branches[0].Condition.Block!;
            Check("expression: in an If it gives true/false", asCondition.Kind == ValueKind.Bool && Eval(asCondition) is true);
            var e1 = Errors();
            var mixFailed = false;
            try { Eval(Expr("{cost} > 1")); } catch (BlockFailedException) { mixFailed = true; }
            Check("expression: true/false in a number slot fails naming it", mixFailed && Errors() == e1 + 1 && LastError().Contains("gives true/false, not a number"));
        }

        // ---- Set Parameter (main thread, errors are counted) ----
        {
            int Errors() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Count(l => l.Contains("] error - ")); }
            string LastError() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Last(l => l.Contains("] error - ")); }
            SetParametersAction SetP(string name, object value)
            {
                var set = new SetParametersAction();
                var row = set.Rows.Items[^1];
                row.Key.SetText(name);
                if (value is BlockModel block) row.Value.BlockSlot.TrySetBlock(block); else row.Value.SetText((string)value);
                return set;
            }
            ExpressionBlock Expr(string text) { var e = new ExpressionBlock(); e.Formula.SetText(text); return e; }

            // runs a List of these blocks, then a ParamRecordBlock that looks up `key`
            string RunList(Dictionary<string, string> parameters, string key, params BlockModel[] blocks)
            {
                var list = new ListBlock();
                var slot = (BlockSlotList)list.Slots[0];
                for (int i = 0; i < blocks.Length; i++) slot.TryInsertBlock(blocks[i], i);
                slot.TryInsertBlock(new ParamRecordBlock { Key = key }, blocks.Length);
                ParamRecordBlock.Seen = null;
                list.SafeRunAsync(new TriggerContext(TriggerPhase.Once, default, parameters)).GetAwaiter().GetResult();
                return ParamRecordBlock.Seen ?? "(didn't run)";
            }

            var given = new Dictionary<string, string> { ["cost"] = "25" };
            Check("set parameter: later blocks see it", RunList(given, "otherCost", SetP("otherCost", "100")) == "100");
            Check("set parameter: typed text fills in parameters", RunList(given, "next", SetP("next", "{cost} and more")) == "25 and more");
            Check("set parameter: a block's value is stored as text", RunList(given, "total", SetP("total", Expr("{cost}*({cost}+1)/2"))) == "325");
            Check("set parameter: one set can use an earlier one", RunList(given, "b", SetP("a", "4"), SetP("b", Expr("{a}*{a}"))) == "16");
            Check("set parameter: replaces a given parameter for the rest of the run", RunList(given, "cost", SetP("cost", "1")) == "1");
            Check("set parameter: the trigger's own parameters aren't changed", given["cost"] == "25" && !given.ContainsKey("otherCost"));
            Check("set parameter: the next run starts without it", RunList(given, "otherCost") == "(missing)");

            var e0 = Errors();
            Check("set parameter: no name fails and stops the run", RunList(given, "x", SetP(" ", "1")) == "(didn't run)" && Errors() == e0 + 1 && LastError().Contains("error - Set Parameters: a row needs a name"));
            Check("set parameter: a name {} can't use fails", RunList(given, "x", SetP("my cost", "1")) == "(didn't run)" && LastError().Contains("'my cost' isn't a valid name"));
            // one scope per run: set four Ifs deep, still there after the Ifs finish
            BlockModel deep = SetP("deep", "found");
            for (int i = 0; i < 4; i++)
            {
                var ifBlock = new IfBlock();
                var branches = (BlockBranchList)ifBlock.Slots[0];
                branches.TryInsertBranch(new FixedCondition { Value = true }, 0);
                branches.Branches[0].Then.TrySetBlock(deep);
                deep = ifBlock;
            }
            Check("set parameter: set deep inside Ifs, seen after them", RunList(given, "deep", deep) == "found");
            var copied = (SetParametersAction)BlockResolver.DeepCopy(SetP("otherCost", Expr("{cost}+1")))!;
            Check("set parameter: saved and loaded keeps its name and value", copied.Rows.Items[0].Key.Text == "otherCost" && RunList(given, "otherCost", copied) == "26");

            // set parameters: name / value rows
            SetParametersAction SetMany(params (string Name, object Value)[] rows)
            {
                var set = new SetParametersAction();
                foreach (var (name, value) in rows)
                {
                    var row = set.Rows.Items[^1];
                    row.Key.SetText(name);
                    if (value is BlockModel block) row.Value.BlockSlot.TrySetBlock(block); else row.Value.SetText((string)value);
                }
                return set;
            }
            var many = SetMany(("a", "4"), ("b", Expr("{a}*{a}")), ("c", "{b}!"));
            Check("set parameters: always one empty row at the end", many.Rows.Items.Count == 4 && many.Rows.Items[^1].IsEmpty);
            Check("set parameters: rows set top to bottom, a later row sees an earlier one", RunList(given, "c", many) == "16!");
            many.Rows.Items[0].Key.SetText("");
            many.Rows.Items[0].Value.SetText("");
            Check("set parameters: an emptied row disappears", many.Rows.Items.Count == 3 && many.Rows.Items[0].Key.Text == "b");
            var manyCopy = (SetParametersAction)BlockResolver.DeepCopy(SetMany(("x", "1"), ("y", Expr("{x}+{cost}"))))!;
            Check("set parameters: saved and loaded keeps its rows", manyCopy.Rows.Items.Count == 3 && RunList(given, "y", manyCopy) == "26");
        }

        // ---- OBS, against a pretend OBS (main thread, blocking instead of awaiting, so console errors can be counted) ----
        {
            int Errors() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Count(l => l.Contains("] error - ")); }
            string LastError() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Last(l => l.Contains("] error - ")); }
            string LastWarning() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.LastOrDefault(l => l.Contains("] warning - ")) ?? ""; }
            T Wait<T>(Task<T> task) => task.GetAwaiter().GetResult();
            bool Until(Func<bool> done)
            {
                for (int i = 0; i < 100; i++)
                {
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    if (done()) return true;
                    Thread.Sleep(30);
                }
                return false;
            }

            using var fake = new FakeObsServer();
            var obsModel = new ObsComponentModel();
            var obs = new ObsManager(obsModel);

            // connecting
            Check("obs: off until asked", !obs.IsConnected && obs.Status == ConnectionStatus.NotConnected);
            var e0 = Errors();
            obsModel.SetConnectionDetails(fake.Address, "wrong");
            obsModel.SetIsEnabled(true);
            Check("obs: wrong password fails with one error", Until(() => obs.Status == ConnectionStatus.Failed) && !obs.IsConnected
                && Errors() == e0 + 1 && LastError().Contains("error - can't connect to obs (authentication failed)"));
            obsModel.SetConnectionDetails(fake.Address, FakeObsServer.Password);
            Check("obs: the right password connects", Until(() => obs.IsConnected && obs.Status == ConnectionStatus.Connected) && obs.StatusMessage == $"Connected to {fake.Address}");
            var nowhereModel = new ObsComponentModel();
            nowhereModel.SetConnectionDetails("127.0.0.1:1", "");
            var nowhere = new ObsManager(nowhereModel);
            var e1 = Errors();
            nowhereModel.SetIsEnabled(true);
            Check("obs: nothing listening fails with one error", Until(() => nowhere.Status == ConnectionStatus.Failed) && Errors() == e1 + 1 && LastError().Contains("nothing at that address"));
            nowhere.Dispose();
            Check("obs: an empty address means the default", new Func<bool>(() => { var m = new ObsComponentModel(); m.SetConnectionDetails("  ", "x"); return m.Address == ObsComponentModel.DefaultAddress; })());

            // reading
            Check("obs: live scene", Wait(obs.TryGetCurrentSceneAsync()) == (true, "Gaming"));
            Check("obs: scenes top to bottom", Wait(obs.TryGetScenesAsync()).Value!.SequenceEqual(new[] { "Gaming", "Just Chatting" }));
            Check("obs: sources of the live scene, top to bottom", Wait(obs.TryGetSourcesAsync("")).Value!.SequenceEqual(new[] { "Cam", "Title" }));
            Check("obs: sources of a named scene", Wait(obs.TryGetSourcesAsync("Just Chatting")).Value!.SequenceEqual(new[] { "Cam" }));

            // the blocks
            StreamAssist.Shell.GlobalContext.ObsManager = obs;
            var ctx = new TriggerContext(TriggerPhase.Once, default, new Dictionary<string, string> { ["user"] = "bob" });
            T Block<T>(T block, params string[] slotTexts) where T : BlockModel
            {
                for (int i = 0; i < slotTexts.Length; i++) ((ValueSlot)block.Slots[i]).SetText(slotTexts[i]);
                return block;
            }
            bool RunObs(ExecutableBlock block) { RecordBlock.Log.Clear(); var list = new ListBlock(); var slot = (BlockSlotList)list.Slots[0]; slot.TryInsertBlock(block, 0); slot.TryInsertBlock(new RecordBlock { Tag = "after" }, 1); list.SafeRunAsync(ctx).GetAwaiter().GetResult(); return RecordBlock.Log.Count == 1; }
            object Eval(ValueBlock block) => block.EvaluateObjectAsync(ctx).GetAwaiter().GetResult();

            Check("obs blocks: get scene / scenes / sources", Eval(new GetObsSceneBlock()) is "Gaming" && Eval(new GetObsScenesBlock()) is "Gaming, Just Chatting" && Eval(Block(new GetObsSourcesBlock(), "")) is "Cam, Title");
            Check("obs blocks: hide a source in the live scene", RunObs(Block(new SetObsSourceVisibleAction { Change = "Hide" }, "", "Title")) && !fake.Scenes["Gaming"][1].Enabled);
            Check("obs blocks: visible condition", Eval(Block(new ObsSourceVisibleCondition(), "", "Title")) is false && Eval(Block(new ObsSourceVisibleCondition(), "Gaming", "Cam")) is true);
            Check("obs blocks: toggle shows it again", RunObs(Block(new SetObsSourceVisibleAction(), "Gaming", "Title")) && fake.Scenes["Gaming"][1].Enabled);
            Check("obs blocks: volume in dB", RunObs(Block(new SetObsVolumeAction(), "Mic", "-20")) && fake.Inputs["Mic"].VolumeDb == -20 && Eval(Block(new GetObsVolumeBlock(), "Mic")) is -20.0);
            Check("obs blocks: volume is kept in OBS's range", RunObs(Block(new SetObsVolumeAction(), "Mic", "40")) && fake.Inputs["Mic"].VolumeDb == 26);
            Check("obs blocks: mute toggles, muted condition", RunObs(Block(new SetObsMuteAction(), "Mic")) && fake.Inputs["Mic"].Muted && Eval(Block(new ObsSourceMutedCondition(), "Mic")) is true);
            Check("obs blocks: unmute", RunObs(Block(new SetObsMuteAction { Change = "Unmute" }, "Mic")) && !fake.Inputs["Mic"].Muted);
            Check("obs blocks: text fills in parameters", RunObs(Block(new SetObsTextAction(), "Title", "{user} raided")) && fake.Inputs["Title"].Settings["text"]!.GetValue<string>() == "bob raided");
            Check("obs blocks: move a source", RunObs(Block(new SetObsTransformAction { Setting = "Position X" }, "", "Title", "100")) && fake.Scenes["Gaming"][1].Transform["positionX"]!.GetValue<double>() == 100
                && Eval(Block(new GetObsTransformBlock { Setting = "Position X" }, "", "Title")) is 100.0);
            Check("obs blocks: width resizes through the scale", RunObs(Block(new SetObsTransformAction { Setting = "Width" }, "", "Title", "800")) && fake.Scenes["Gaming"][1].Transform["scaleX"]!.GetValue<double>() == 2);
            Check("obs blocks: a never changed setting reads its default", Eval(Block(new GetObsSourceSettingBlock { As = "Int" }, "Title", "opacity")) is 100);
            Check("obs blocks: a typed setting keeps the setting's kind", RunObs(Block(new SetObsSourceSettingAction(), "Title", "opacity", "50")) && fake.Inputs["Title"].Settings["opacity"]!.GetValueKind() == System.Text.Json.JsonValueKind.Number
                && RunObs(Block(new SetObsSourceSettingAction(), "Title", "vertical", "true")) && fake.Inputs["Title"].Settings["vertical"]!.GetValueKind() == System.Text.Json.JsonValueKind.True);
            Check("obs blocks: a new text setting stays text", RunObs(Block(new SetObsSourceSettingAction(), "Title", "font_face", "Arial")) && fake.Inputs["Title"].Settings["font_face"]!.GetValue<string>() == "Arial");
            Check("obs blocks: get a setting as text or a number", Eval(Block(new GetObsSourceSettingBlock(), "Title", "text")) is "bob raided" && Eval(Block(new GetObsSourceSettingBlock { As = "Int" }, "Title", "opacity")) is 50);
            // every switch remembers the scene it leaves (in variables), Return to Previous Scene goes back like undo
            var obsData = new DataModel();
            obsData.EnsureBuiltInTables();
            StreamAssist.Shell.GlobalContext.DataModel = obsData;
            fake.Scenes["BRB"] = new();
            string History() => string.Join(",", ObsSceneMemory.Load(obsData));
            Check("obs blocks: switch scene, remembering the one left", RunObs(Block(new SetObsSceneAction(), "Just Chatting")) && fake.CurrentScene == "Just Chatting"
                && History() == "Gaming" && obsData.VariableKind("previousScene1") == ValueKind.Text && obsData.FindTable("misc") == null);
            Check("obs scenes: switching to the scene already live remembers nothing", RunObs(Block(new SetObsSceneAction(), "Just Chatting")) && History() == "Gaming");
            Check("obs scenes: Gaming -> Just Chatting -> BRB, most recent first", RunObs(Block(new SetObsSceneAction(), "BRB")) && History() == "Just Chatting,Gaming");
            Check("obs scenes: Return goes back one at a time, like undo", RunObs(new ReturnToPreviousSceneAction()) && fake.CurrentScene == "Just Chatting" && History() == "Gaming"
                && RunObs(new ReturnToPreviousSceneAction()) && fake.CurrentScene == "Gaming" && History() == "");
            var eNone = Errors();
            Check("obs scenes: nothing left to go back to fails with one error", !RunObs(new ReturnToPreviousSceneAction()) && Errors() == eNone + 1 && LastError().Contains("none remembered"));
            foreach (var scene in new[] { "BRB", "Gaming", "BRB", "Just Chatting", "Gaming", "BRB", "Gaming" }) RunObs(Block(new SetObsSceneAction(), scene));
            Check("obs scenes: only the last 5 are kept", History() == "BRB,Gaming,Just Chatting,BRB,Gaming");
            fake.CurrentScene = "BRB"; // changed in OBS itself
            Check("obs scenes: a scene picked in OBS counts as the one left, and Return skips one that's already live",
                RunObs(Block(new SetObsSceneAction(), "Gaming")) && History() == "BRB,BRB,Gaming,Just Chatting,BRB"
                && RunObs(new ReturnToPreviousSceneAction()) && fake.CurrentScene == "BRB" && History() == "BRB,Gaming,Just Chatting,BRB"
                && RunObs(new ReturnToPreviousSceneAction()) && fake.CurrentScene == "Gaming" && History() == "Just Chatting,BRB");


            // failures: one error with OBS's reason, and the run stops
            var e2 = Errors();
            Check("obs blocks: a missing source fails with OBS's reason and stops", !RunObs(Block(new SetObsSourceVisibleAction(), "", "Nope")) && Errors() == e2 + 1
                && LastError().Contains("error - can't show/hide 'Nope' (No source was found by the name of `Nope`.)"));

            // obs going away: one warning, failed, and requests say obs isn't connected
            fake.Drop();
            Check("obs: closing says so once", Until(() => !obs.IsConnected && obs.Status == ConnectionStatus.Failed) && LastWarning().Contains("warning - obs disconnected (obs is shutting down)"));
            var e3 = Errors();
            Check("obs: not connected fails with one error", !RunObs(Block(new SetObsSceneAction(), "Gaming")) && Errors() == e3 + 1 && LastError().Contains("error - can't switch to scene 'Gaming', obs not connected"));

            StreamAssist.Shell.GlobalContext.ObsManager = null;
            StreamAssist.Shell.GlobalContext.DataModel = null;
            obs.Dispose();
        }

        // ---- Spotify, against a pretend Spotify (blocking like OBS's, so console errors can be counted) ----
        {
            int Errors() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Count(l => l.Contains("] error - ")); }
            string LastError() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Last(l => l.Contains("] error - ")); }
            string LastWarning() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.LastOrDefault(l => l.Contains("] warning - ")) ?? ""; }
            int Prompts() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Count(l => l.Contains("] prompt - ")); }
            string LastPrompt() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.LastOrDefault(l => l.Contains("] prompt - ")) ?? ""; }
            T Wait<T>(Task<T> task) => task.GetAwaiter().GetResult();
            bool Until(Func<bool> done)
            {
                for (int i = 0; i < 100; i++)
                {
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    if (done()) return true;
                    Thread.Sleep(30);
                }
                return false;
            }

            // links
            Check("spotify: a share link is a song", SpotifyManager.TryParseLink("https://open.spotify.com/track/4cOdK2wGLETKBW3PvgPWqT?si=abc", out var link1) && link1 == "spotify:track:4cOdK2wGLETKBW3PvgPWqT");
            Check("spotify: spotify: links and other countries' links", SpotifyManager.TryParseLink("spotify:playlist:37i9dQZF1DX", out var link2) && link2 == "spotify:playlist:37i9dQZF1DX"
                && SpotifyManager.TryParseLink(" open.spotify.com/intl-de/album/abc123 ", out var link3) && link3 == "spotify:album:abc123");
            Check("spotify: words and other sites aren't links", !SpotifyManager.TryParseLink("never gonna give you up", out _) && !SpotifyManager.TryParseLink("https://example.com/track/abc", out _));

            using var fake = new FakeSpotifyServer();
            var store = new MemorySpotifyLoginStore();
            var spotifyModel = new SpotifyComponentModel();
            var spotify = new SpotifyManager(spotifyModel, store, TimeSpan.FromMilliseconds(50)); // checks the login often

            // connecting: no client id, then the first login through the browser
            Check("spotify: off until asked", !spotify.IsConnected && spotify.Status == ConnectionStatus.NotConnected);
            var s0 = Errors();
            spotifyModel.SetIsEnabled(true);
            Check("spotify: no client id fails with one error", Until(() => spotify.Status == ConnectionStatus.Failed) && Errors() == s0 + 1 && LastError().Contains("error - can't connect to spotify, no client id"));
            var p0 = Prompts();
            spotifyModel.SetClientId(" my-app ");
            Check("spotify: the first time it shows a login link", Until(() => Prompts() == p0 + 1) && LastPrompt().Contains("prompt - spotify login, open: http://localhost:")
                && LastPrompt().Contains("client_id=my-app") && LastPrompt().Contains("redirect_uri=http%3A%2F%2F127.0.0.1%3A8974%2Fcallback")
                && LastPrompt().Contains("code_challenge_method=S256") && LastPrompt().Contains("playlist-modify-private") && spotify.StatusMessage == "Waiting for login");
            var state = System.Text.RegularExpressions.Regex.Match(LastPrompt(), "state=([0-9a-f]+)").Groups[1].Value;
            using (var browser = new HttpClient())
            {
                var favicon = Wait(browser.GetAsync("http://127.0.0.1:8974/favicon.ico"));
                var page = Wait(browser.GetStringAsync($"http://127.0.0.1:8974/callback?code=the-code&state={state}"));
                Check("spotify: the browser coming back logs in", favicon.StatusCode == HttpStatusCode.NotFound && page.Contains("logged in to Spotify")
                    && Until(() => spotify.Status == ConnectionStatus.Connected) && spotify.StatusMessage == "Connected to Budder");
            }
            Check("spotify: the login is kept with the app that made it", store.Login is { ClientId: "my-app", RefreshToken: FakeSpotifyServer.GoodRefreshToken } && store.Login.AccessToken == fake.Token);
            spotifyModel.SetIsEnabled(false);
            Check("spotify: disconnecting keeps the login", Until(() => spotify.Status == ConnectionStatus.NotConnected) && !spotify.IsConnected && store.Login.RefreshToken != "");
            var p1 = Prompts();
            spotifyModel.SetIsEnabled(true);
            Check("spotify: connecting again uses the saved login", Until(() => spotify.Status == ConnectionStatus.Connected) && Prompts() == p1);

            // the blocks
            StreamAssist.Shell.GlobalContext.SpotifyManager = spotify;
            var sctx = new TriggerContext(TriggerPhase.Once, default, new Dictionary<string, string> { ["input"] = "sandstorm" });
            T Block<T>(T block, params string[] slotTexts) where T : BlockModel
            {
                for (int i = 0; i < slotTexts.Length; i++) ((ValueSlot)block.Slots[i]).SetText(slotTexts[i]);
                return block;
            }
            bool RunSpotify(ExecutableBlock block) { RecordBlock.Log.Clear(); var list = new ListBlock(); var slot = (BlockSlotList)list.Slots[0]; slot.TryInsertBlock(block, 0); slot.TryInsertBlock(new RecordBlock { Tag = "after" }, 1); list.SafeRunAsync(sctx).GetAwaiter().GetResult(); return RecordBlock.Log.Count == 1; }
            object Eval(ValueBlock block) => block.EvaluateObjectAsync(sctx).GetAwaiter().GetResult();
            int Sent(string start) { lock (fake.Requests) return fake.Requests.Count(r => r.StartsWith(start)); }

            Check("spotify blocks: nothing playing reads as not playing", Eval(new GetSpotifyPlaybackBlock { Setting = "Playing" }) is false && Eval(new GetSpotifyPlaybackBlock()) is "");
            Check("spotify blocks: play a searched song on the computer when nothing is playing", RunSpotify(Block(new PlaySpotifyAction(), "rick astley"))
                && Sent("PUT me/player/play?device_id=pc {\"uris\":[\"spotify:track:never\"]}") == 1 && fake.Song!["name"]!.GetValue<string>() == "Never Gonna Give You Up");
            Check("spotify blocks: play a link where it's playing", RunSpotify(Block(new PlaySpotifyAction(), "https://open.spotify.com/track/sand?si=1"))
                && Sent("PUT me/player/play {\"uris\":[\"spotify:track:sand\"]}") == 1);
            Check("spotify blocks: play one of your playlists by name", RunSpotify(Block(new PlaySpotifyAction { Find = "Playlist" }, " stream LIKES "))
                && Sent("PUT me/player/play {\"context_uri\":\"spotify:playlist:likes\"}") == 1);
            var s1 = Errors();
            Check("spotify blocks: a playlist you don't have fails once and stops", !RunSpotify(Block(new PlaySpotifyAction { Find = "Playlist" }, "Nope"))
                && Errors() == s1 + 1 && LastError().Contains("error - can't play 'Nope' (you have no playlist by that name)"));
            Check("spotify blocks: a search with no song fails", !RunSpotify(Block(new QueueSpotifySongAction(), "asdfgh")) && LastError().Contains("error - can't queue 'asdfgh' (no song found)"));
            Check("spotify blocks: queue a redeem's text", RunSpotify(Block(new QueueSpotifySongAction(), "{input}")) && fake.Queue.SequenceEqual(new[] { "spotify:track:sand" }));

            Check("spotify blocks: what's playing", Eval(new GetSpotifyPlaybackBlock()) is "Sandstorm" && Eval(new GetSpotifyPlaybackBlock { Setting = "Artist" }) is "Darude, Someone"
                && Eval(new GetSpotifyPlaybackBlock { Setting = "Playing" }) is true && Eval(new GetSpotifyPlaybackBlock { Setting = "Progress Seconds" }) is 61
                && Eval(new GetSpotifyPlaybackBlock { Setting = "Length Seconds" }) is 225 && Eval(new GetSpotifyPlaybackBlock { Setting = "Link" }) is "https://open.spotify.com/track/sand");
            Check("spotify blocks: pause", RunSpotify(new SetSpotifyPlaybackAction { Change = "Pause" }) && !fake.Playing);
            Check("spotify blocks: pausing while paused asks nothing", RunSpotify(new SetSpotifyPlaybackAction { Change = "Pause" }) && Sent("PUT me/player/pause") == 1);
            Check("spotify blocks: toggle resumes", RunSpotify(new SetSpotifyPlaybackAction()) && fake.Playing && Sent("PUT me/player/play {}") + Sent("PUT me/player/play") >= 1);
            Check("spotify blocks: next", RunSpotify(new SetSpotifyPlaybackAction { Change = "Next" }) && Sent("POST me/player/next") == 1);
            Check("spotify blocks: volume is kept to 0-100", RunSpotify(Block(new SetSpotifyVolumeAction(), "150")) && fake.Volume == 100 && Eval(new GetSpotifyPlaybackBlock { Setting = "Volume" }) is 100);
            Check("spotify blocks: shuffle toggles, on while on asks nothing", RunSpotify(new SetSpotifyShuffleAction()) && fake.Shuffle
                && RunSpotify(new SetSpotifyShuffleAction { Change = "On" }) && Sent("PUT me/player/shuffle") == 1);
            Check("spotify blocks: repeat the song", RunSpotify(new SetSpotifyRepeatAction { Repeat = "Song" }) && fake.Repeat == "track" && Eval(new GetSpotifyPlaybackBlock { Setting = "Repeat" }) is "Song");
            Check("spotify blocks: add the song playing to a playlist", RunSpotify(Block(new AddToSpotifyPlaylistAction(), "Stream Likes", "")) && fake.Playlist.SequenceEqual(new[] { "spotify:track:never", "spotify:track:sand" }));
            Check("spotify blocks: a song already in the playlist isn't added again", RunSpotify(Block(new AddToSpotifyPlaylistAction(), "spotify:playlist:likes", "rick")) && fake.Playlist.Count == 2);
            Check("spotify blocks: find a song", Eval(Block(new FindSpotifySongBlock { Setting = "Length Seconds" }, "rick astley")) is 213
                && Eval(Block(new FindSpotifySongBlock(), "spotify:track:sand")) is "Sandstorm");
            var savedPlay = (PlaySpotifyAction)BlockResolver.DeepCopy(Block(new PlaySpotifyAction { Find = "Album" }, "{rest}"))!;
            Check("spotify blocks: saved and loaded keeps its settings", savedPlay.Find == "Album" && ((ValueSlot)savedPlay.Slots[0]).Text == "{rest}"
                && ((GetSpotifyPlaybackBlock)BlockResolver.DeepCopy(new GetSpotifyPlaybackBlock { Setting = "Shuffle" })!).Kind == ValueKind.Bool);

            // logins: a request never refreshes or retries, a refused token disconnects (the login is kept for Retry)
            fake.Token = "rotated";
            var s2 = Errors();
            var refreshes = fake.Refreshes;
            var volumeBefore = fake.Volume;
            Check("spotify: a refused token fails the request, once disconnected, no refresh", !RunSpotify(Block(new SetSpotifyVolumeAction(), "30")) && fake.Volume == volumeBefore
                && Until(() => spotify.Status == ConnectionStatus.Failed) && spotify.StatusMessage == "Disconnected" && !spotify.IsConnected && fake.Refreshes == refreshes
                && LastWarning().Contains("warning - spotify disconnected (login refused)") && Errors() == s2 + 1 && LastError().Contains("error - can't set spotify volume (login refused)"));
            var warnings = AppLog.Lines.Count(l => l.Contains("] warning - "));
            Check("spotify: later requests just say it isn't connected", !RunSpotify(Block(new SetSpotifyVolumeAction(), "30")) && Errors() == s2 + 2
                && LastError().Contains("error - can't set spotify volume, spotify not connected") && AppLog.Lines.Count(l => l.Contains("] warning - ")) == warnings);
            spotifyModel.Reconnect();
            Check("spotify: retry uses the saved login again", Until(() => spotify.Status == ConnectionStatus.Connected) && store.Login.RefreshToken == FakeSpotifyServer.GoodRefreshToken
                && RunSpotify(Block(new SetSpotifyVolumeAction(), "30")) && fake.Volume == 30);

            // the background check refreshes a login with under 15 minutes left, a refused refresh token is a lost login
            fake.ExpiresIn = 600;
            spotifyModel.Reconnect();
            Check("spotify: the background check refreshes a login that's running out", Until(() => spotify.Status == ConnectionStatus.Connected)
                && Until(() => fake.Refreshes >= refreshes + 3) && store.Login.AccessToken == fake.Token);
            fake.RefuseRefresh = true;
            Check("spotify: a lost login says so once", Until(() => spotify.Status == ConnectionStatus.Failed) && spotify.StatusMessage == "Login expired"
                && LastWarning().Contains("warning - spotify login expired, press retry") && store.Login.RefreshToken == "" && !spotify.IsConnected);
            fake.ExpiresIn = 3600;

            StreamAssist.Shell.GlobalContext.SpotifyManager = null;
            spotify.Dispose();
        }

        // ---- bind labels ----
        {
            var bind = BindableModel.CreateDefault();
            int labelEvents = 0, labelChanges = 0;
            bind.LabelChanged += () => labelEvents++;
            bind.Changed += () => labelChanges++;
            Check("label: none by default, and not saved", bind.Label == "" && !JsonSerializer.Serialize(bind).Contains("label"));
            bind.SetLabel("On Hold");
            Check("label: setting it says so (keys / bind boxes / saving listen)", bind.Label == "On Hold" && labelEvents == 1 && labelChanges == 1);
            bind.SetLabel("On Hold");
            Check("label: setting the same one does nothing", labelEvents == 1);
            var labelled = JsonSerializer.Deserialize<BindableModel>(JsonSerializer.Serialize(bind))!;
            Check("label: saved and loaded", labelled.Label == "On Hold" && BindableModel.DeepCopy(bind).Label == "On Hold");
            bind.SetLabel("");
            Check("label: cleared is not saved again", !JsonSerializer.Serialize(bind).Contains("label"));
            bind.SetLabel("Again");
            bind.Root.RemoveBlock(bind.Root.Block!);
            Check("label: removing the last block clears it", bind.Label == "" && !bind.HasBlock);
            bind.SetLabel("Nothing bound");
            Check("label: no blocks, no label", bind.Label == "");

            // the editor's label box: only a saved binding with blocks can be labelled
            var editor = new StreamAssist.Components.Blocks.UI.BindableEditorViewModel(new StreamAssist.Shell.WindowLayoutModel(), "nothing");
            BindableModel? saved = null;
            editor.ShowDraft("Key: X", draft => { saved = draft; return true; });
            Check("editor label: a draft can't be labelled yet", !editor.CanEditBindLabel);
            ((BlockSlot)editor.Bindable!.Root.Block!.Slots[0]).TrySetBlock(new RecordBlock { Tag = "x" });
            Check("editor label: once the draft is saved it can", saved != null && editor.CanEditBindLabel);
            editor.BindLabel = "My bind";
            Check("editor label: typing sets the binding's label", saved!.Label == "My bind");
            saved.Root.RemoveBlock(saved.Root.Block!);
            Check("editor label: removing the last block clears it and locks the box", saved.Label == "" && editor.BindLabel == "" && !editor.CanEditBindLabel);
            editor.Dispose();

            // the key editor: the default On Pressed only comes with selecting an unbound key
            var keyEditor = new StreamAssist.Components.Keyboard.UI.KeybindViewModel(new StreamAssist.Shell.WindowLayoutModel());
            var keyConfig = new KeybindConfigModel("Test", null);
            var key7 = new StreamAssist.Components.Keyboard.UI.KeyViewModel("7", 103, 0, 0, 1, 1, keyEditor);
            keyEditor.SetSelectedKey(key7, null, keyConfig);
            Check("key editor: selecting an unbound key starts with On Pressed", keyEditor.Bindable?.Root.Block is OnPressedBlock);
            ((BlockSlot)keyEditor.Bindable!.Root.Block!.Slots[0]).TrySetBlock(new RecordBlock { Tag = "x" });
            var boundKey = keyConfig.Keybinds[103];
            Check("key editor: its first change binds it", keyEditor.Bindable == boundKey);
            boundKey.Root.RemoveBlock(boundKey.Root.Block!);
            Check("key editor: removing every block unbinds it and nothing comes back", !keyConfig.Keybinds.ContainsKey(103) && keyEditor.Bindable != null && keyEditor.Bindable.Root.Block == null);
            keyEditor.Bindable!.Root.TrySetBlock(new RecordBlock { Tag = "y" });
            Check("key editor: dropping a block in binds it again", keyConfig.Keybinds.ContainsKey(103));
            keyEditor.SetSelectedKey(new StreamAssist.Components.Keyboard.UI.KeyViewModel("8", 104, 0, 0, 1, 1, keyEditor), null, keyConfig);
            Check("key editor: selecting another unbound key starts with On Pressed again", keyEditor.Bindable?.Root.Block is OnPressedBlock);
            keyEditor.Dispose();
        }

        // ---- Voicemeeter channels and Twitch info blocks (no Voicemeeter or Twitch here: they say so and stop) ----
        {
            int Errors() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Count(l => l.Contains("] error - ")); }
            string LastError() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Last(l => l.Contains("] error - ")); }
            string Names(int edition) => string.Join(", ", StreamAssist.Components.Audio.Background.VoicemeeterChannels.For(edition).Select(c => $"{c.Name}={c.Prefix}"));

            // voicemeeter's channels, as each edition's window names them
            Check("voicemeeter: banana's channels", Names(2) == "Stereo Input 1=Strip[0], Stereo Input 2=Strip[1], Stereo Input 3=Strip[2], Voicemeeter Input=Strip[3], Voicemeeter AUX=Strip[4], A1=Bus[0], A2=Bus[1], A3=Bus[2], B1=Bus[3], B2=Bus[4]");
            Check("voicemeeter: basic's channels", Names(1) == "Stereo Input 1=Strip[0], Stereo Input 2=Strip[1], Voicemeeter Input=Strip[2], A1=Bus[0], B1=Bus[1]");
            Check("voicemeeter: potato's channels", Names(3).StartsWith("Stereo Input 1=Strip[0]") && Names(3).Contains("Voicemeeter VAIO3=Strip[7]") && Names(3).EndsWith("B3=Bus[7]"));
            var banana = StreamAssist.Components.Audio.Background.VoicemeeterChannels.For(2);
            Check("voicemeeter: inputs and buses lists", StreamAssist.Components.Audio.Background.VoicemeeterChannels.Names(banana, inputs: true).Count == 5
                && StreamAssist.Components.Audio.Background.VoicemeeterChannels.Names(banana, inputs: false).SequenceEqual(new[] { "A1", "A2", "A3", "B1", "B2" }));

            var vmManager = new StreamAssist.Components.Audio.Background.VoicemeeterManager(new StreamAssist.Components.Audio.Model.AudioComponentModel());
            var v0 = Errors();
            Check("voicemeeter: not connected says so", !vmManager.TryGetMute("B1", out _) && Errors() == v0 + 1 && LastError().Contains("can't read voicemeeter Bus[3].Mute, not connected"));
            Check("voicemeeter: an unknown channel says so", !vmManager.TrySetMute("Stereo Input 9", true) && LastError().Contains("voicemeeter has no channel 'Stereo Input 9'"));
            Check("voicemeeter: a route goes from an input to a bus", !vmManager.TrySetRoute("B1", "A1", true) && LastError().Contains("it goes from an input to an output bus"));
            vmManager.Dispose();

            var mute = BlockResolver.DeepCopy(new MuteVoicemeeterAction { Channel = "B2", MuteSound = "m.mp3", UnmuteSound = "u.mp3" }) as MuteVoicemeeterAction;
            Check("voicemeeter: the mute block saves its channel and sounds", mute is { Channel: "B2", MuteSound: "m.mp3", UnmuteSound: "u.mp3", SetType: "Toggle" });
            var vmList = new ListBlock();
            ((BlockSlotList)vmList.Slots[0]).TryInsertBlock(new MuteVoicemeeterAction(), 0);
            ((BlockSlotList)vmList.Slots[0]).TryInsertBlock(new RecordBlock { Tag = "after" }, 1);
            RecordBlock.Log.Clear();
            var v1 = Errors();
            vmList.SafeRunAsync(new TriggerContext(TriggerPhase.Once, default)).GetAwaiter().GetResult();
            Check("voicemeeter: without voicemeeter the block fails once and stops", RecordBlock.Log.Count == 0 && Errors() == v1 + 1 && LastError().Contains("Set Voicemeeter Mute: voicemeeter not set up"));

            // twitch info blocks pick their type from the setting
            Check("twitch info: settings and kinds", TwitchInfos.Names(TwitchInfos.Stream).SequenceEqual(new[] { "Title", "Category", "Live", "Viewers", "Uptime Minutes", "Followers" })
                && TwitchInfos.Find(TwitchInfos.Follower, "Following")!.Kind == ValueKind.Bool && TwitchInfos.Find(TwitchInfos.User, "Account Age Days")!.Kind == ValueKind.Int);
            var stream = new GetStreamInfoBlock { Setting = "Live" };
            Check("twitch info: Live reads true/false", stream.Kind == ValueKind.Bool);
            stream.Setting = "Viewers";
            Check("twitch info: Viewers reads a whole number", stream.Kind == ValueKind.Int);
            var follow = (GetFollowerInfoBlock)BlockResolver.DeepCopy(new GetFollowerInfoBlock { Setting = "Followed On" })!;
            Check("twitch info: saved and loaded keeps the setting and type", follow.Setting == "Followed On" && follow.Kind == ValueKind.Text);
            Check("twitch info: user starts as whoever triggered it", ((ValueSlot)new GetTwitchUserBlock().Slots[0]).Text == "{userLogin}" && ((ValueSlot)new ShoutoutAction().Slots[0]).Text == "{userLogin}");

            // twitch blocks without twitch: one error each, the run stops
            var chatbot = new ChatbotManager(Chatbot());
            StreamAssist.Shell.GlobalContext.ChatbotManager = chatbot;
            string RunTwitch(BlockModel block)
            {
                RecordBlock.Log.Clear();
                var list = new ListBlock();
                ((BlockSlotList)list.Slots[0]).TryInsertBlock(block, 0);
                ((BlockSlotList)list.Slots[0]).TryInsertBlock(new RecordBlock { Tag = "after" }, 1);
                list.SafeRunAsync(new TriggerContext(TriggerPhase.Once, default, new Dictionary<string, string> { ["userLogin"] = "viewer" })).GetAwaiter().GetResult();
                return RecordBlock.Log.Count == 0 ? LastError() : "ran on";
            }
            Check("twitch: unban, not connected", RunTwitch(new UnbanUserAction()).Contains("can't unban 'viewer', twitch not connected"));
            Check("twitch: chat mode, not connected", RunTwitch(new SetChatModeAction { Mode = "Emote Only", Change = "On" }).Contains("can't set emote only, twitch not connected"));
            Check("twitch: chat mode duration has to be a number", RunTwitch(Block(new SetChatModeAction(), "lots")).Contains("Set Chat Mode: Duration 'lots' isn't a whole number"));
            Check("twitch: clip link name has to be valid", RunTwitch(new CreateClipAction { ParameterName = "my link" }).Contains("'my link' isn't a valid name"));
            Check("twitch: run ad keeps twitch's lengths", RunTwitch(Block(new RunAdAction(), "500")).Contains("500s not allowed, 30 to 180"));
            Check("twitch: stream info, not connected", RunTwitch(Block(new SetStreamInfoAction(), "New title")).Contains("can't set stream title, twitch not connected"));
            StartPollAction Poll(params string[] choices)
            {
                var poll = Block(new StartPollAction(), "Best snack?");
                foreach (var c in choices) poll.Choices.Items[^1].SetText(c);
                return poll;
            }
            Check("twitch: poll choices grow like Join's parts", Poll("Chips", "Fruit").Choices.Items.Count == 3);
            Check("twitch: poll needs 2 to 5 choices", RunTwitch(Poll("Chips")).Contains("Start Poll: 1 choices, twitch takes 2 to 5"));
            Check("twitch: poll keeps twitch's lengths", RunTwitch(Block(Poll("Chips", "Fruit"), "Best snack?", "5")).Contains("5s not allowed, 15 to 1800"));
            Check("twitch: poll, not connected", RunTwitch(Poll("Chips", "Fruit")).Contains("can't start poll, twitch not connected"));
            var savedPoll = (StartPollAction)BlockResolver.DeepCopy(Poll("Chips", "Fruit"))!;
            Check("twitch: poll saved and loaded keeps its choices", savedPoll.Choices.Items.Select(i => i.Text).SequenceEqual(new[] { "Chips", "Fruit", "" }) && savedPoll.Title.Text == "Best snack?");
            var prediction = Block(new StartPredictionAction(), "Win?");
            foreach (var o in new[] { "Yes", "No" }) prediction.Outcomes.Items[^1].SetText(o);
            Check("twitch: prediction, not connected", RunTwitch(prediction).Contains("can't start prediction, twitch not connected"));
            Check("twitch: resolve needs a winner", RunTwitch(new EndPredictionAction()).Contains("End Prediction: Resolve needs a Winner"));
            Check("twitch: end poll, not connected", RunTwitch(new EndPollAction()).Contains("can't end poll, twitch not connected"));
            Check("twitch: vip, not connected", RunTwitch(new SetVipAction()).Contains("can't add vip 'viewer', twitch not connected"));
            Check("twitch: moderator, not connected", RunTwitch(new SetModeratorAction { Change = "Remove" }).Contains("can't remove moderator 'viewer', twitch not connected"));
            StreamAssist.Shell.GlobalContext.ChatbotManager = null;
            chatbot.Dispose();

            T Block<T>(T block, params string[] texts) where T : BlockModel
            {
                for (int i = 0; i < texts.Length; i++) ((ValueSlot)block.Slots[i]).SetText(texts[i]);
                return block;
            }
        }

        // ---- Parse Parameter spacing ----
        {
            var m = Chatbot();
            var top = (MatchTextChatBlock)m.Root.Block!;
            var so = new ParameterChatBlock { ParameterName = "target" };
            var soBind = new BindableModel(); soBind.Root.TrySetBlock(new ParamRecordBlock { Key = "target" }); so.Target.TryBind(soBind);
            top.Rows.AddRow("!so").Target.Child.TrySetBlock(so);
            var add = new ParameterChatBlock { ParameterName = "command" };
            var content = new ParameterChatBlock { ParameterName = "content", Reads = ParameterChatBlock.RestOfMessage };
            var cb = new BindableModel(); cb.Root.TrySetBlock(new ParamRecordBlock { Key = "content" }); content.Target.TryBind(cb);
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

        // tree: List[ OnPressed(Rec A), OnReleased(Rec B), Rec C ]
        BindableModel Build()
        {
            var b = new BindableModel();
            var list = new ListBlock();
            b.Root.TrySetBlock(list);
            var listSlot = (BlockSlotList)list.Slots[0];
            var p = new OnPressedBlock(); ((BlockSlot)p.Slots[0]).TrySetBlock(new RecordBlock { Tag = "A" });
            var r = new OnReleasedBlock(); ((BlockSlot)r.Slots[0]).TrySetBlock(new RecordBlock { Tag = "B" });
            listSlot.TryInsertBlock(p, 0);
            listSlot.TryInsertBlock(r, 1);
            listSlot.TryInsertBlock(new RecordBlock { Tag = "C" }, 2);
            return b;
        }

        async Task<string> Run(BindableModel b, Action<BindableModel> trigger)
        {
            RecordBlock.Log.Clear();
            trigger(b);
            await Task.Delay(50);
            return string.Join("", RecordBlock.Log);
        }

        var b = Build();
        Check("start runs pressed + plain", await Run(b, x => x.TriggerStart()) == "AC");
        Check("stop runs released + plain", await Run(b, x => x.TriggerStop()) == "BC");
        Check("once runs both", await Run(b, x => x.TriggerOnce()) == "ABC");

        // disable OnPressed: A skipped, but A itself not disabled
        var listSlot0 = (BlockSlotList)b.Root.Block!.Slots[0];
        listSlot0.Blocks[0].SetIsDisabled(true);
        Check("disabled parent skips child", await Run(b, x => x.TriggerOnce()) == "BC");
        listSlot0.Blocks[0].SetIsDisabled(false);

        b.SetIsDisabled(true);
        Check("disabled bindable runs nothing", await Run(b, x => x.TriggerOnce()) == "");
        b.SetIsDisabled(false);

        // error in the middle stops the rest, but is caught
        var boom = new ListBlock();
        var boomSlot = (BlockSlotList)boom.Slots[0];
        boomSlot.TryInsertBlock(new RecordBlock { Tag = "1" }, 0);
        boomSlot.TryInsertBlock(new RecordBlock { Tag = "X", Throw = true }, 1);
        boomSlot.TryInsertBlock(new RecordBlock { Tag = "2" }, 2);
        var bb = new BindableModel(); bb.Root.TrySetBlock(boom);
        Check("error stops list and is caught", await Run(bb, x => x.TriggerOnce()) == "1X");

        // Changed bubbles from deep inside
        int changes = 0; b.Changed += () => changes++;
        ((RecordBlock)((BlockSlot)listSlot0.Blocks[0].Slots[0]).Block!).Tag2 = "changed";
        Check("nested change bubbles to bindable", changes == 1);

        // json round trip
        var json = JsonSerializer.Serialize(b);
        var copy = JsonSerializer.Deserialize<BindableModel>(json)!;
        Check("round trip keeps behavior", await Run(copy, x => x.TriggerOnce()) == "ABC");
        Check("round trip json stable", JsonSerializer.Serialize(copy) == json);
        Check("json has no Name/Category", !json.Contains("\"Name\"") && !json.Contains("Category"));
        log(json);

        var d = BindableModel.CreateDefault();
        Check("default has On Pressed", d.Root.Block is OnPressedBlock);

        // ContainsSlot
        var outer = new ListBlock(); var inner = new OnPressedBlock();
        ((BlockSlotList)outer.Slots[0]).TryInsertBlock(inner, 0);
        Check("block contains its descendant slot", outer.ContainsSlot(inner.Slots[0]));
        Check("block doesn't contain unrelated slot", !inner.ContainsSlot(outer.Slots[0]));

        // cancellation on dispose: a slow block
        var slow = new BindableModel(); slow.Root.TrySetBlock(new RecordBlock { Tag = "S", DelayMs = 500 });
        RecordBlock.Log.Clear(); slow.TriggerOnce(); slow.Dispose(); await Task.Delay(700);
        Check("dispose cancels running tree quietly", string.Join("", RecordBlock.Log) == "S");


        // ---- If block ----
        IfBlock MakeIf(params (bool? cond, string? then)[] branches)
        {
            var ifb = new IfBlock();
            var list = (BlockBranchList)ifb.Slots[0];
            foreach (var (cond, then) in branches)
            {
                list.TryInsertBranch(new FixedCondition { Value = cond ?? false }, list.Branches.Count);
                var br = list.Branches[^1];
                if (cond == null) br.Condition.RemoveBlock(br.Condition.Block!); // leave condition empty
                if (then != null) br.Then.TrySetBlock(new RecordBlock { Tag = then });
            }
            ((BlockSlot)ifb.Slots[1]).TrySetBlock(new RecordBlock { Tag = "E" });
            return ifb;
        }
        async Task<string> RunIf(IfBlock ifb) { var bb2 = new BindableModel(); bb2.Root.TrySetBlock(ifb); return await Run(bb2, x => x.TriggerOnce()); }

        Check("if: first true wins", await RunIf(MakeIf((false, "1"), (true, "2"), (true, "3"))) == "2");
        Check("if: none true runs else", await RunIf(MakeIf((false, "1"), (false, "2"))) == "E");
        Check("if: no branches runs else", await RunIf(MakeIf()) == "E");
        Check("if: empty condition counts false", await RunIf(MakeIf((null, "1"), (true, "2"))) == "2");

        var ifb3 = MakeIf((true, null));
        var bl3 = (BlockBranchList)ifb3.Slots[0];
        bl3.Branches[0].Condition.RemoveBlock(bl3.Branches[0].Condition.Block!);
        Check("if: branch removed when condition and then both empty", bl3.Branches.Count == 0);

        var ifb4 = MakeIf((true, "1"));
        var bl4 = (BlockBranchList)ifb4.Slots[0];
        bl4.Branches[0].Condition.RemoveBlock(bl4.Branches[0].Condition.Block!);
        Check("if: branch kept while then has a block", bl4.Branches.Count == 1 && bl4.Branches[0].Then.Block != null);
        bl4.Branches[0].Then.RemoveBlock(bl4.Branches[0].Then.Block!);
        Check("if: then removed too -> branch gone", bl4.Branches.Count == 0);

        var ifb5 = MakeIf((false, "1"), (true, "2"));
        var bif = new BindableModel(); bif.Root.TrySetBlock(ifb5);
        var ifJson = JsonSerializer.Serialize(bif);
        var bifCopy = JsonSerializer.Deserialize<BindableModel>(ifJson)!;
        Check("if: round trip keeps behavior", await Run(bifCopy, x => x.TriggerOnce()) == "2" && JsonSerializer.Serialize(bifCopy) == ifJson);
        log(ifJson);

        var bl5 = (BlockBranchList)ifb5.Slots[0];
        Check("if: contains its branch slots (no self-drop)", ifb5.ContainsSlot(bl5.Branches[1].Then) && ifb5.ContainsSlot(bl5.Branches[0].Condition));
        Check("if: list only accepts conditions", !bl5.TryInsertBranch(new RecordBlock(), 0));
        int ifChanges = 0; bif.Changed += () => ifChanges++;
        ((FixedCondition)bl5.Branches[0].Condition.Block!).Value = true;
        Check("if: change inside branch bubbles", ifChanges == 1);

        var dIf = new ListBlock(); var dIfB = new IfBlock(); ((BlockSlotList)dIf.Slots[0]).TryInsertBlock(dIfB, 0);
        var disIf = MakeIf((true, "1")); disIf.SetIsDisabled(true);
        Check("if: disabled if runs nothing", await RunIf(disIf) == "");
        var disCond = MakeIf((true, "1")); ((BlockBranchList)disCond.Slots[0]).Branches[0].Condition.Block!.SetIsDisabled(true);
        Check("if: disabled condition counts false", await RunIf(disCond) == "E");


        // ---- empty bindables remove themselves ----
        {
            var cfg = new StreamAssist.Components.Keyboard.Model.KeybindConfigModel("t", null);
            var unbound = new List<ushort>(); cfg.KeyUnbound += (vk, _) => unbound.Add(vk);
            var kb = BindableModel.CreateDefault();
            cfg.TryBindKey(65, kb);
            kb.Root.RemoveBlock(kb.Root.Block!);
            Check("removing root unbinds the key", !cfg.Keybinds.ContainsKey(65) && unbound.SequenceEqual(new ushort[] { 65 }));

            var kb2 = BindableModel.CreateDefault(); cfg.TryBindKey(66, kb2);
            var kbInner = (BlockSlot)kb2.Root.Block!.Slots[0]; kbInner.TrySetBlock(new RecordBlock()); kbInner.RemoveBlock(kbInner.Block!);
            Check("removing a nested block keeps the binding", cfg.Keybinds.ContainsKey(66));
            kb2.Clear();
            Check("Clear() unbinds the key", !cfg.Keybinds.ContainsKey(66));

            var oldJson = "{\"name\":\"x\",\"bindables\":{\"55\":{\"isDisabled\":false,\"root\":null},\"70\":{\"isDisabled\":false,\"root\":{\"type\":\"OnPressedBlock\",\"parameters\":{\"child\":null,\"isDisabled\":false}}}}}";
            var loaded = JsonSerializer.Deserialize<StreamAssist.Components.Keyboard.Model.KeybindConfigModel>(oldJson)!;
            Check("empty bindables dropped on load", !loaded.Keybinds.ContainsKey(55) && loaded.Keybinds.ContainsKey(70));
            loaded.Keybinds[70].Clear();
            Check("loaded bindable can still unbind itself", loaded.Keybinds.Count == 0);
            Check("saved json has no empty bindings", !JsonSerializer.Serialize(loaded).Contains("\"root\":null"));

            var copyCfg = new StreamAssist.Components.Keyboard.Model.KeybindConfigModel("c", new() { [80] = BindableModel.CreateDefault(), [81] = new BindableModel() });
            Check("copied config drops empties", copyCfg.Keybinds.ContainsKey(80) && !copyCfg.Keybinds.ContainsKey(81));
            copyCfg.Keybinds[80].Clear();
            Check("copied config bindable unbinds itself", copyCfg.Keybinds.Count == 0);
        }


        // ---- key names ----
        {
            ushort V(string n) => StreamAssist.Components.Keyboard.UI.DeviceDisplayHelper.TryParseKeyName(n, out var v) ? v : (ushort)0xFFFF;
            Check("key names", V("shift") == 0x10 && V("Ctrl") == 0x11 && V("alt") == 0x12 && V("t") == 0x54 && V("F5") == 0x74
                && V(";") == 0xBA && V("[") == 0xDB && V("/") == 0xBF && V("numpad1") == 0x61 && V("esc") == 0x1B && V(" Space ") == 0x20 && V("5") == 0x35);
            foreach (var n in new[]{"/","-","+","*",".","divide"}) log($"  {n} -> {V(n):X}");
            Check("bad key name rejected", V("nonsense") == 0xFFFF && V("") == 0xFFFF);
        }

        // ---- logic conditions ----
        {
            async Task<bool> Eval(ConditionBlock c) => await c.EvaluateAsync(new TriggerContext(TriggerPhase.Once, default));
            T WithList<T>(T c, params bool[] values) where T : ConditionBlock
            {
                var list = (BlockSlotList)c.Slots[0];
                foreach (var v in values) list.TryInsertBlock(new FixedCondition { Value = v }, list.Blocks.Count);
                return c;
            }
            var and = new AndCondition();
            var or = new OrCondition();
            var not = new NotCondition();
            Check("empty logic blocks are false", !await Eval(and) && !await Eval(or) && !await Eval(not));
            Check("and", await Eval(WithList(new AndCondition(), true, true, true)) && !await Eval(WithList(new AndCondition(), true, false, true)));
            Check("or", await Eval(WithList(new OrCondition(), false, true)) && !await Eval(WithList(new OrCondition(), false, false)));
            ((BlockSlot)not.Slots[0]).TrySetBlock(new FixedCondition { Value = false });
            Check("not", await Eval(not));
            Check("logic list only takes conditions", !((BlockSlotList)and.Slots[0]).TryInsertBlock(new RecordBlock(), 0));
            var nested = WithList(new AndCondition(), true);
            ((BlockSlotList)nested.Slots[0]).TryInsertBlock(not, 1);
            var holder = new IfBlock(); ((BlockBranchList)holder.Slots[0]).TryInsertBranch(nested, 0);
            var hb = new BindableModel(); hb.Root.TrySetBlock(holder);
            var hj = JsonSerializer.Serialize(hb);
            Check("nested logic round trip", JsonSerializer.Serialize(JsonSerializer.Deserialize<BindableModel>(hj)!) == hj);
        }

        // ---- key state + keybind manager + key pressed ----
        {
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var input = new KeyboardInputManager();
            var held = (HashSet<(string, ushort)>)typeof(KeyboardInputManager).GetField("_keysDown", flags)!.GetValue(input)!;
            void Raise(string name, string dev, ushort vk)
            {
                var d = (Action<string, ushort>?)typeof(KeyboardInputManager).GetField(name, flags)!.GetValue(input);
                d?.Invoke(dev, vk);
            }
            void Down(string dev, ushort vk) { if (held.Add((dev, vk))) Raise("KeyDown", dev, vk); }
            void Up(string dev, ushort vk) { held.Remove((dev, vk)); Raise("KeyUp", dev, vk); }
            StreamAssist.Shell.GlobalContext.KeyboardInputManager = input;

            var list = Keyboards();
            var dev1 = new InputDeviceModel("dev1", "p", "m", 1, 2);
            var dev2 = new InputDeviceModel("dev2", "p", "m", 3, 4);
            list.AttachDevices(new List<InputDeviceModel> { dev1, dev2 });
            list.CreateSlot(out var slot);
            var cfg = slot.SelectedConfig!; // the slot's keys in the Default config
            slot.TrySetSelectedDevice(dev1);
            var km = new KeybindManager(list, input);

            // T: if shift held -> "S" else "T"; on release -> "R"
            var tBind = new BindableModel();
            var tList = new ListBlock(); tBind.Root.TrySetBlock(tList);
            var press = new OnPressedBlock(); var ifb = new IfBlock();
            var kp = new KeyPressedCondition { Key = "shift" };
            ((BlockBranchList)ifb.Slots[0]).TryInsertBranch(kp, 0);
            ((BlockBranchList)ifb.Slots[0]).Branches[0].Then.TrySetBlock(new RecordBlock { Tag = "S" });
            ((BlockSlot)ifb.Slots[1]).TrySetBlock(new RecordBlock { Tag = "T" });
            ((BlockSlot)press.Slots[0]).TrySetBlock(ifb);
            var rel = new OnReleasedBlock(); ((BlockSlot)rel.Slots[0]).TrySetBlock(new RecordBlock { Tag = "R" });
            ((BlockSlotList)tList.Slots[0]).TryInsertBlock(press, 0); ((BlockSlotList)tList.Slots[0]).TryInsertBlock(rel, 1);
            cfg.TryBindKey(0x54, tBind);

            async Task<string> Do(Action a) { RecordBlock.Log.Clear(); a(); await Task.Delay(50); return string.Join("", RecordBlock.Log); }
            Check("T alone -> else", await Do(() => Down("dev1", 0x54)) == "T");
            Check("release -> on released", await Do(() => Up("dev1", 0x54)) == "R");
            Check("shift+T -> then", await Do(() => { Down("dev1", KeyboardDevices.LeftShift); Down("dev1", 0x54); }) == "S");
            Check("repeat key down ignored", await Do(() => Down("dev1", 0x54)) == "");
            await Do(() => { Up("dev1", 0x54); Up("dev1", KeyboardDevices.LeftShift); });
            Check("other keyboard doesn't trigger", await Do(() => Down("dev2", 0x54)) == "");
            Up("dev2", 0x54);
            Check("right shift on other keyboard counts for Any keyboard", await Do(() => { Down("dev2", KeyboardDevices.RightShift); Down("dev1", 0x54); }) == "S");
            Up("dev1", 0x54); Up("dev2", KeyboardDevices.RightShift);

            Down("dev2", 0x41);
            var heldOk = input.IsKeyDown(0x41) && input.IsKeyDown("dev2", 0x41) && !input.IsKeyDown("dev1", 0x41);
            Up("dev2", 0x41);
            Check("IsKeyDown any/specific", heldOk && !input.IsKeyDown(0x41));

            // switching config/device takes effect immediately (no cached lookup)
            list.Configs.TryCreate("c2"); // a copy of Default, switched to
            var cfg2 = slot.SelectedConfig!;
            cfg2.UnbindKey(0x54);
            var b2 = BindableModel.CreateDefault(); ((BlockSlot)b2.Root.Block!.Slots[0]).TrySetBlock(new RecordBlock { Tag = "2" }); cfg2.TryBindKey(0x54, b2);
            Check("config switch used right away", await Do(() => Down("dev1", 0x54)) == "2");
            Up("dev1", 0x54);
            slot.TrySetSelectedDevice(dev2);
            Check("device switch used right away", await Do(() => Down("dev2", 0x54)) == "2");
            Up("dev2", 0x54);

            // a bind that changes config while the trigger loop runs doesn't crash
            var swap = new BindableModel(); swap.Root.TrySetBlock(new SwapConfigBlock { List = list, To = "Default" });
            cfg2.TryBindKey(0x55, swap);
            Check("bind can switch config mid-trigger", await Do(() => Down("dev2", 0x55)) == "" && slot.SelectedConfig == cfg);
            Up("dev2", 0x55);
            km.Dispose();

            // tab configs: one config for every keyboard, switching swaps every slot's keys at once
            list.CreateSlot(out var slotB);
            Check("configs: a new slot gets its own keys in the config in use", slotB.Id != slot.Id && slotB.SelectedConfig != null && slotB.SelectedConfig != slot.SelectedConfig);
            slotB.SelectedConfig!.TryBindKey(0x41, BindableModel.CreateDefault());
            list.Configs.TrySelect("c2");
            Check("configs: switching swaps every slot's keys", slot.SelectedConfig == cfg2 && !slotB.SelectedConfig!.Keybinds.ContainsKey(0x41));
            list.Configs.TrySelect("Default");
            Check("configs: and back", slot.SelectedConfig == cfg && slotB.SelectedConfig!.Keybinds.ContainsKey(0x41));
            var listJson = JsonSerializer.Serialize(list);
            var listLoaded = JsonSerializer.Deserialize<KeyboardListModel>(listJson)!;
            Check("configs: saved and loaded with the one in use, each slot finds its keys by id", listLoaded.Configs.SelectedName == "Default"
                && listLoaded.Configs.Names.SequenceEqual(new[] { "Default", "c2" }) && listLoaded.Slots[1].SelectedConfig!.Keybinds.ContainsKey(0x41)
                && listLoaded.Slots[0].SelectedConfig!.Keybinds.ContainsKey(0x54) && JsonSerializer.Serialize(listLoaded) == listJson);
            list.TryDeleteSlot(slotB);
            Check("configs: a deleted slot's keys go, in every config", list.Configs.Items.All(c => !c.Keyboards.ContainsKey(slotB.Id)));
            Check("configs: deleting the last config leaves the keyboards with no keys", list.Configs.TryDelete("c2") && list.Configs.TryDelete("Default")
                && list.Configs.Items.Count == 0 && slot.SelectedConfig == null);
            list.CreateSlot(out var slotC);
            Check("configs: with none, a new slot has no keys either, the first config gives them some", slotC.SelectedConfig == null
                && list.Configs.TryCreate("Fresh") && slotC.SelectedConfig != null && slotC.SelectedConfig.Keybinds.Count == 0 && slot.SelectedConfig!.Keybinds.Count == 0);

            // either side: Shift / Ctrl / Alt typed by name mean either, a side's own code only that side
            Down("dev1", KeyboardDevices.RightCtrl);
            Check("held keys: ctrl means either side", input.IsKeyDown(0x11) && input.IsKeyDown("dev1", 0x11) && input.IsKeyDown(KeyboardDevices.RightCtrl)
                && !input.IsKeyDown(KeyboardDevices.LeftCtrl) && !input.IsKeyDown("dev2", 0x11));
            Up("dev1", KeyboardDevices.RightCtrl);

            // keyboards plugged in and removed: slots let go of a removed one (keeping it saved) and find it again
            var plug = Keyboards();
            var kbA = new InputDeviceModel("pathA", "Keys", "Maker", 1, 2);
            var kbB = new InputDeviceModel("pathB", "Keys", "Maker", 3, 4);
            plug.AttachDevices(new List<InputDeviceModel> { kbA, kbB });
            plug.CreateSlot(out var plugSlot);
            plugSlot.TrySetSelectedDevice(kbA);
            int devicesChanged = 0;
            plug.DevicesChanged += () => devicesChanged++;
            plug.AttachDevices(new List<InputDeviceModel> { kbB });
            Check("keyboards: a removed keyboard leaves its slot, still saved", plugSlot.SelectedDevice == null && JsonSerializer.Serialize(plugSlot).Contains("\"selectedDevice\":\"pathA\"") && devicesChanged == 1);
            plug.AttachDevices(new List<InputDeviceModel> { kbB, kbA });
            Check("keyboards: plugged back in, the slot has it again", plugSlot.SelectedDevice == kbA && devicesChanged == 2);
            plug.AttachDevices(new List<InputDeviceModel> { kbB });
            plugSlot.TrySetSelectedDevice(null);
            plug.AttachDevices(new List<InputDeviceModel> { kbA, kbB });
            Check("keyboards: picking none forgets the one that was waiting", plugSlot.SelectedDevice == null && !JsonSerializer.Serialize(plugSlot).Contains("pathA"));

            // layouts: the keyboard picture or every key code, saved per slot; switching remakes the keys, binds stay
            Check("layouts: a slot starts on the keyboard picture", plugSlot.Layout == KeyboardSlotModel.KeyboardLayout && KeyboardSlotModel.Layouts.Count == 2);
            int layoutChanges = 0;
            plugSlot.LayoutChanged += _ => layoutChanges++;
            Check("layouts: an unknown one is refused", !plugSlot.TrySetLayout("Piano") && plugSlot.Layout == KeyboardSlotModel.KeyboardLayout && layoutChanges == 0);
            Check("layouts: switching says so and is saved", plugSlot.TrySetLayout(KeyboardSlotModel.AllKeysLayout) && layoutChanges == 1
                && JsonSerializer.Deserialize<KeyboardSlotModel>(JsonSerializer.Serialize(plugSlot))!.Layout == KeyboardSlotModel.AllKeysLayout
                && JsonSerializer.Deserialize<KeyboardSlotModel>("{\"layout\":\"Piano\"}")!.Layout == KeyboardSlotModel.KeyboardLayout);
            var (allKeys, _, _) = DeviceDisplayHelper.InitGenericDisplayKeys(25, 3);
            Check("layouts: all keys has F13-F24 and media keys, no mouse buttons or either-side codes", allKeys.Any(k => k.VK == 0x7C && k.Label == "F13") && allKeys.Any(k => k.VK == 0x87)
                && allKeys.Any(k => k.Label == "MEDIA\nPLAY\nPAUSE" && k.VK == 0xB3 && k.X == 19 * 28 && k.Y == 5 * 28 && k.W == k.H) && allKeys.All(k => k.VK > 0x06 && k.VK is < 0x10 or > 0x12)
                && allKeys.Select(k => k.VK).Distinct().Count() == allKeys.Count && allKeys.Any(k => k.VK == KeyboardDevices.RightShift));
            var layoutConfig = plugSlot.SelectedConfig!;
            layoutConfig.TryBindKey(0x7C, BindableModel.CreateDefault());
            var layoutView = new KeyboardSlotViewModel(plugSlot, input, []);
            Check("layouts: the slot shows every key code", layoutView.Keys.Count == allKeys.Count && layoutView.MenuLayout == KeyboardSlotModel.AllKeysLayout
                && layoutView.Keys.Single(k => k.VK == 0x7C).IsBound);
            Check("layouts: only bound keys make edit buttons", layoutView.Keys.Single(k => k.VK == 0x7C).EditButtonsContext != null
                && layoutView.Keys.Where(k => k.VK != 0x7C).All(k => k.EditButtonsContext == null));
            layoutView.MenuLayout = KeyboardSlotModel.KeyboardLayout;
            Check("layouts: the dropdown switches the slot back, the keys are remade", plugSlot.Layout == KeyboardSlotModel.KeyboardLayout && layoutView.Keys.All(k => k.VK != 0x7C)
                && layoutView.Keys.Any(k => k.VK == KeyboardDevices.LeftShift));
            layoutView.Dispose();

            // the keyboards on this PC (read only, logged for a look, how many depends on the PC)
            foreach (var device in KeyboardInputManager.Devices) log($"  keyboard here: {device.GetDisplayLabel()}  [{device.DevicePath}]");
        }

        // ---- raw keys and keyboard connections ----
        {
            Check("raw keys: the two shifts, ctrls and alts are told apart", KeyboardDevices.KeyCode(0x10, 0x2A, false) == KeyboardDevices.LeftShift
                && KeyboardDevices.KeyCode(0x10, 0x36, false) == KeyboardDevices.RightShift && KeyboardDevices.KeyCode(0x11, 0x1D, false) == KeyboardDevices.LeftCtrl
                && KeyboardDevices.KeyCode(0x11, 0x1D, true) == KeyboardDevices.RightCtrl && KeyboardDevices.KeyCode(0x12, 0x38, false) == KeyboardDevices.LeftAlt
                && KeyboardDevices.KeyCode(0x12, 0x38, true) == KeyboardDevices.RightAlt);
            Check("raw keys: numpad Enter is its own key", KeyboardDevices.KeyCode(0x0D, 0x1C, true) == KeyboardDevices.NumpadEnter && KeyboardDevices.KeyCode(0x0D, 0x1C, false) == 0x0D);
            Check("raw keys: numpad keys with Num Lock off are still numpad keys", KeyboardDevices.KeyCode(0x24, 0x47, false) == 0x67 && KeyboardDevices.KeyCode(0x2E, 0x53, false) == 0x6E
                && KeyboardDevices.KeyCode(0x24, 0x47, true) == 0x24 && KeyboardDevices.KeyCode(0x25, 0x4B, true) == 0x25);
            Check("raw keys: windows' fake shifts and 'no key' are ignored, other keys pass", KeyboardDevices.KeyCode(0x10, 0x2A, true) == null && KeyboardDevices.KeyCode(0xFF, 0, false) == null
                && KeyboardDevices.KeyCode(0x41, 0x1E, false) == 0x41);
            Check("raw keys: the layout and names know the new keys", DeviceDisplayHelper.VKToKeyboardLabel(KeyboardDevices.RightShift) == "Shift" && DeviceDisplayHelper.VKToKeyboardLabel(KeyboardDevices.NumpadEnter) == "Enter"
                && DeviceDisplayHelper.TryParseKeyName("shift", out var shiftCode) && KeyboardDevices.CodesFor(shiftCode).SequenceEqual(new[] { KeyboardDevices.LeftShift, KeyboardDevices.RightShift })
                && DeviceDisplayHelper.TryParseKeyName("rshift", out var rshift) && rshift == KeyboardDevices.RightShift
                && DeviceDisplayHelper.TryParseKeyName("numpadenter", out var npEnter) && npEnter == KeyboardDevices.NumpadEnter);

            var g1 = Guid.NewGuid();
            var g2 = Guid.NewGuid();
            var none = new Guid("00000000-0000-0000-ffff-ffffffffffff");
            var interfaces = new List<KeyboardInterface>
            {
                new(@"\\?\HID#VID_1&PID_2&MI_00#a", 1, 2, "Maker", "Keys", g1),
                new(@"\\?\HID#VID_1&PID_2&MI_01&Col01#b", 1, 2, "Maker", "Keys", g1),
                new(@"\\?\HID#VID_1&PID_2&MI_00#c", 1, 2, "Maker", "Keys", g2),
                new(@"\\?\ACPI#laptop", 9, 9, "", "", none),
                new(@"\\?\HID#other", 7, 7, "Other", "Board", null),
            };
            var groups = KeyboardDevices.Group(interfaces);
            Check("keyboards: one per physical keyboard, every connection of it counts", groups.Count == 4 && groups[0].Paths.Count == 2 && groups[0].Device.DevicePath.EndsWith("#a"));
            Check("keyboards: identical keyboards stay separate and are numbered", groups[1].Device.DevicePath.EndsWith("#c") && groups[1].Device.GetDisplayLabel().EndsWith("#2")
                && !groups[0].Device.GetDisplayLabel().Contains('#'));
            Check("keyboards: no known container means a keyboard of its own", groups[2].Paths.Count == 1 && groups[3].Paths.Count == 1 && groups[2].Device.ProductName == "Unknown");
            Check("keyboards: the main path skips sub-collections", KeyboardDevices.Group(new[] { interfaces[1], interfaces[0] })[0].Device.DevicePath.EndsWith("#a"));
            var again = KeyboardDevices.Group(interfaces, groups.Select(g => g.Device).ToList());
            Check("keyboards: reading them again keeps the same models", again.Select(g => g.Device).SequenceEqual(groups.Select(g => g.Device)));
        }


        // ---- eventsub message parsing (examples shaped like the twitch docs) ----
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

        // ---- chatbot model + match text rows ----
        // a target is filled with either a binding or a chat block
        void Fill(ChatTarget target, object then)
        {
            if (then is BindableModel bindable) target.TryBind(bindable);
            else target.Child.TrySetBlock((ChatBlock)then);
        }
        MatchTextChatBlock M(params (string Pattern, object Then)[] rows)
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
        ParameterChatBlock P(string name, string reads, object then) { var b = new ParameterChatBlock { ParameterName = name, Reads = reads }; Fill(b.Target, then); return b; }
        BindableModel B(string tag) { var b = new BindableModel(); b.Root.TrySetBlock(new ChatLogBlock { Tag = tag }); return b; }

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
            rowBinding.Root.RemoveBlock(rowBinding.Root.Block!);
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
            var both = "{\"rows\":[{\"pattern\":\"z\",\"bindable\":{\"root\":{\"type\":\"ChatLogBlock\",\"parameters\":{\"tag\":\"Z\"}}},\"child\":{\"type\":\"MatchTextChatBlock\",\"parameters\":{\"rows\":[]}}}]}";
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
            log(chatJson);
        }

        // ---- chat matching ----
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
            var pb = new BindableModel(); pb.Root.TrySetBlock(new ParamRecordBlock { Key = "user" });
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
        {
            TwitchEvent Ev(string type, string evJson) => EventSubMessage.Parse(
                "{\"metadata\":{\"message_id\":\"x\",\"message_type\":\"notification\",\"message_timestamp\":\"t\",\"subscription_type\":\"" + type + "\",\"subscription_version\":\"1\"},"
                + "\"payload\":{\"subscription\":{\"id\":\"s\",\"type\":\"" + type + "\"},\"event\":" + evJson + "}}").Event!;
            BindableModel Rec(string key) { var b = new BindableModel(); b.Root.TrySetBlock(new ParamRecordBlock { Key = key }); return b; }
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

            Check("all event slots exist", model.Events.Select(e => e.Name).SequenceEqual(new[] { "Follow", "Sub", "Gift Subs", "Cheer", "Raid", "Stream Online", "Stream Offline" }));
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
            var unknown = JsonSerializer.Deserialize<ChatbotComponentModel>("{\"events\":{\"nope\":{\"root\":{\"type\":\"ParamRecordBlock\",\"parameters\":{\"key\":\"x\"}}}}}")!;
            Check("unknown saved event is skipped", unknown.Events.All(e => e.Target.Bindable == null));

            int eventChanges = 0; model.Changed += () => eventChanges++;
            hydrate.SetTitle("Hydrate");
            raidSlot.Target.Bindable!.SetIsDisabled(true);
            Check("redeem + event changes reach the model (saving)", eventChanges >= 2);
        }

        // ---- connection settings ----
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

        // ---- value blocks + value slots ----
        {
            var ctx = new TriggerContext(TriggerPhase.Once, default, new Dictionary<string, string> { ["user"] = "Viewer", ["viewers"] = "57", ["price"] = "2.5", ["bad"] = "lots", ["mod"] = "true" });
            var noParams = new TriggerContext(TriggerPhase.Start, default);

            // conversion table
            Check("int -> double automatic", ValueTypes.CanConvert(ValueKind.Int, ValueKind.Double) && !ValueTypes.CanConvert(ValueKind.Double, ValueKind.Int));
            Check("anything -> text automatic", ValueTypes.CanConvert(ValueKind.Bool, ValueKind.Text) && ValueTypes.CanConvert(ValueKind.Char, ValueKind.Text) && ValueTypes.CanConvert(ValueKind.Double, ValueKind.Text));
            Check("text -> anything needs a block", !ValueTypes.CanConvert(ValueKind.Text, ValueKind.Int) && !ValueTypes.CanConvert(ValueKind.Text, ValueKind.Bool));
            Check("bool/char don't become numbers", !ValueTypes.CanConvert(ValueKind.Bool, ValueKind.Int) && !ValueTypes.CanConvert(ValueKind.Char, ValueKind.Int));
            Check("number text uses '.'", ValueTypes.ToText(1.5) == "1.5" && ValueTypes.ToText(true) == "true" && ValueTypes.ToText(7) == "7");
            Check("parse", ValueTypes.TryParse(" 42 ", ValueKind.Int, out var p1) && (int)p1 == 42 && !ValueTypes.TryParse("4.2", ValueKind.Int, out _)
                && ValueTypes.TryParse("4.2", ValueKind.Double, out var p2) && (double)p2 == 4.2 && !ValueTypes.TryParse("4,2", ValueKind.Double, out _)
                && ValueTypes.TryParse("TRUE", ValueKind.Bool, out var p3) && (bool)p3 && ValueTypes.TryParse("x", ValueKind.Char, out _) && !ValueTypes.TryParse("xy", ValueKind.Char, out _));

            // typed values (with {name} filled in first)
            var textSlot = new ValueSlot<string>("Message"); textSlot.SetText("hi {user}");
            Check("typed text fills in parameters", await textSlot.EvaluateAsync(ctx) == "hi Viewer");
            var intSlot = new ValueSlot<int>("Amount"); intSlot.SetText("{viewers}");
            Check("typed int can be a parameter", await intSlot.EvaluateAsync(ctx) == 57);
            var doubleSlot = new ValueSlot<double>("Price"); doubleSlot.SetText("{price}");
            Check("typed double", await doubleSlot.EvaluateAsync(ctx) == 2.5);

            // which blocks fit
            Check("text slot takes an int block", textSlot.BlockSlot.Accepts(IntParam("x")));
            Check("int slot refuses a text block", !intSlot.BlockSlot.Accepts(new StringBuilderBlock()));
            Check("double slot takes an int block", doubleSlot.BlockSlot.Accepts(IntParam("x")));
            Check("value slots refuse actions", !textSlot.BlockSlot.Accepts(new RecordBlock()));
            Check("bool blocks still fit condition slots", new BlockSlot("", typeof(ConditionBlock)).Accepts(new EqualsCondition()));

            // a block wins over typed text, a disabled block falls back to it
            var viewersBlock = IntParam("viewers");
            textSlot.BlockSlot.TrySetBlock(viewersBlock);
            Check("block value converts to the slot's kind", await textSlot.EvaluateAsync(ctx) == "57");
            viewersBlock.SetIsDisabled(true);
            Check("disabled block falls back to typed text", await textSlot.EvaluateAsync(ctx) == "hi Viewer");
            var doubleFromInt = new ValueSlot<double>("D"); doubleFromInt.BlockSlot.TrySetBlock(IntParam("viewers"));
            Check("int block into a double slot", await doubleFromInt.EvaluateAsync(ctx) == 57.0);

            // Equals: different kinds aren't equal, numbers are one family, typed text takes the other side's kind
            Check("5 == 5.0", EqualsCondition.AreEqual(ValueKind.Int, 5, ValueKind.Double, 5.0));
            Check("typed '50' equals int 50", EqualsCondition.AreEqual(null, "50", ValueKind.Int, 50));
            Check("typed 'abc' isn't an int", !EqualsCondition.AreEqual(null, "abc", ValueKind.Int, 5));
            Check("text '5' != int 5 (both blocks)", !EqualsCondition.AreEqual(ValueKind.Text, "5", ValueKind.Int, 5));
            Check("bool vs text not equal", !EqualsCondition.AreEqual(ValueKind.Bool, true, ValueKind.Text, "true"));
            Check("both typed compare as text", EqualsCondition.AreEqual(null, "a", null, "a") && !EqualsCondition.AreEqual(null, "a", null, "A"));
            Check("typed 'TRUE' equals bool true", EqualsCondition.AreEqual(ValueKind.Bool, true, null, "TRUE"));

            T Slot<T>(BlockModel b, int i) where T : BlockSlotBase => (T)b.Slots[i];

            var equals = new EqualsCondition();
            Slot<ValueSlot>(equals, 0).BlockSlot.TrySetBlock(IntParam("viewers"));
            Slot<ValueSlot>(equals, 1).SetText("57");
            Check("Equals block: viewers == 57", await equals.EvaluateAsync(ctx));

            var ignoreCase = new EqualsCondition { Case = "Ignore Case" };
            Slot<ValueSlot>(ignoreCase, 0).SetText("{user}"); Slot<ValueSlot>(ignoreCase, 1).SetText("VIEWER");
            Check("Equals: Ignore Case", await ignoreCase.EvaluateAsync(ctx));
            ignoreCase.Case = "Match Case";
            Check("Equals: Match Case (the default) doesn't", !await ignoreCase.EvaluateAsync(ctx) && new EqualsCondition().Case == "Match Case"
                && BlockResolver.DeepCopy(new EqualsCondition { Case = "Ignore Case" }) is EqualsCondition { Case: "Ignore Case" });

            Check("Convert to Bool", (bool)await MakeConvert("Bool", "{mod}").EvaluateObjectAsync(ctx));

            // Expression's functions (Round is gone)
            double? Fn(string text) => StreamAssist.Core.Utilities.Formula.TryEvaluate(text, out var n, out _) ? n.Value : null;
            Check("expression functions", Fn("round(2.5)") == 3 && Fn("round(-2.5)") == -3 && Fn("floor(2.7)") == 2 && Fn("ceil(2.1)") == 3 && Fn("truncate(-2.7)") == -2
                && Fn("abs(-4)") == 4 && Fn("log10(1000)") == 3 && Fn("pow(2, 10)") == 1024 && Fn("log10(0)") == null && Fn("max(1, 7, 3)") == 7 && Fn("min(2.5, 1)") == 1 && Fn("ROUND(10 / 4.0) * 2") == 6);
            Check("expression functions: rounding gives a whole number, mistakes fail", StreamAssist.Core.Utilities.Formula.TryEvaluate("round(7 / 2.0)", out var rounded, out _) && rounded.IsWhole && rounded.Value == 4
                && !StreamAssist.Core.Utilities.Formula.TryEvaluate("sqrt(4)", out _, out var noFunction) && noFunction.Contains("no function 'sqrt'")
                && Fn("round(1, 2)") == null && Fn("max()") == null && Fn("abs(true)") == null && Fn("round(2") == null);

            // string builder: a text with {name}s, and rows below it that only this block sees
            var sb = new StringBuilderBlock();
            Check("string builder starts with one empty row", sb.Values.Items.Count == 1 && sb.Values.Items[0].IsEmpty);
            sb.Text.SetText("{user} raided with {1}{2}");
            var row1 = sb.Values.Items[^1];
            row1.Key.SetText("1");
            Check("typing a name in the last row adds an empty one below", sb.Values.Items.Count == 2);
            row1.Value.BlockSlot.TrySetBlock(IntParam("viewers"));
            var row2 = sb.Values.Items[^1];
            row2.Key.SetText("2");
            row2.Value.SetText("! ({1} total)");
            Check("string builder fills in its rows (a row can use an earlier one) and the run's parameters", await sb.EvaluateAsync(ctx) == "Viewer raided with 57! (57 total)");
            Check("string builder's rows are only for that block", !ctx.Parameters.ContainsKey("1") && ctx.Format("{1}") == "{1}");
            row1.Value.RemoveBlock(row1.Value.BlockSlot.Block!);
            row1.Key.SetText("");
            Check("an emptied row disappears", sb.Values.Items.Count == 2 && await sb.EvaluateAsync(ctx) == "Viewer raided with {1}! ({1} total)");
            var sbJson = JsonSerializer.Serialize(BlockResolver.GetData(sb));
            var sbLoaded = (StringBuilderBlock)BlockResolver.Resolve(JsonSerializer.Deserialize<PendingBlockData>(sbJson)!)!;
            Check("string builder round trip (the trailing empty row isn't saved)", JsonSerializer.Serialize(BlockResolver.GetData(sbLoaded)) == sbJson
                && sbLoaded.Values.Items.Count == 2 && await sbLoaded.EvaluateAsync(ctx) == "Viewer raided with {1}! ({1} total)");
            Check("string builder rows know their block (for errors)", sbLoaded.Values.Items.All(i => i.Key.Owner == sbLoaded && i.Value.Owner == sbLoaded));

            Check("Convert to Int / Char / Double", (int)await MakeConvert("Int", "{viewers}").EvaluateObjectAsync(ctx) == 57
                && (char)await MakeConvert("Char", "!").EvaluateObjectAsync(ctx) == '!' && (double)await MakeConvert("Double", "{price}").EvaluateObjectAsync(ctx) == 2.5);

            // saving
            var saved = new ConsoleMessageAction();
            Slot<ValueSlot>(saved, 0).SetText("typed");
            Slot<ValueSlot>(saved, 0).BlockSlot.TrySetBlock(sbLoaded);
            var savedJson = JsonSerializer.Serialize(BlockResolver.GetData(saved));
            var loadedBlock = BlockResolver.Resolve(JsonSerializer.Deserialize<PendingBlockData>(savedJson)!)!;
            Check("value slots round trip (typed text + block)", JsonSerializer.Serialize(BlockResolver.GetData(loadedBlock)) == savedJson && savedJson.Contains("\"text\":\"typed\""));
            Check("block list headings: general blocks by what they do", new RandomNumberBlock().Group == BlockGroup.Math && new StringBuilderBlock().Group == BlockGroup.Text
                && new EqualsCondition().Group == BlockGroup.Logic && new ConvertBlock().Group == BlockGroup.Parameters
                && new SetParametersAction().Group == BlockGroup.Parameters && new IfBlock().Group == BlockGroup.Control);
            Check("block list headings: integrations by their component folder", new GetObsSceneBlock().Group == BlockGroup.Obs && new ObsSourceMutedCondition().Group == BlockGroup.Obs
                && new SendChatMessageAction().Group == BlockGroup.Twitch && new GetStreamInfoBlock().Group == BlockGroup.Twitch && new MatchTextChatBlock().Group == BlockGroup.Chat
                && new MuteVoicemeeterAction().Group == BlockGroup.Voicemeeter && new PlaySpotifyAction().Group == BlockGroup.Spotify
                && new PlaySoundAction().Group == BlockGroup.App && new ConsoleMessageAction().Group == BlockGroup.App && new KeyPressedCondition().Group == BlockGroup.App);
            Check("block list headings: every block has one", BlockResolver.DiscoverAll().All(b => Enum.IsDefined(b.Group)));
            Check("block cards: value blocks show their type", new BlockCardViewModel(new StringBuilderBlock()).TypeText == "Text" && new BlockCardViewModel(new EqualsCondition()).TypeText == "Bool"
                && new BlockCardViewModel(new ConvertBlock()).TypeText == "Any type" && new BlockCardViewModel(new IfBlock()).TypeText == "");

            // collapsing headings: kept in the window layout, every open list follows it, a search shows everything
            var layout = new StreamAssist.Shell.WindowLayoutModel();
            var palette = new BlockPaletteViewModel(layout, BlockGroup.Control, BlockGroup.Twitch);
            var twitchGroup = palette.Groups.Single(g => g.Group == BlockGroup.Twitch);
            Check("block list: headings in order, expanded at first", palette.Groups.Select(g => g.Title).SequenceEqual(new[] { "Control", "Twitch" }) && twitchGroup.ShowCards && twitchGroup.Heading == "Twitch ▾");
            var otherPalette = new BlockPaletteViewModel(layout, BlockGroup.Twitch);
            twitchGroup.HeadingPressedCommand.Execute(null);
            Check("block list: clicking a heading collapses it, in every list, and it's saved", !twitchGroup.ShowCards && twitchGroup.Heading == "Twitch ▸" && !otherPalette.Groups[0].ShowCards
                && layout.IsBlockGroupCollapsed("Twitch") && JsonSerializer.Serialize(layout).Contains("\"collapsedBlockGroups\":[\"Twitch\"]"));
            Check("block list: a loaded layout keeps it collapsed", !new BlockPaletteViewModel(JsonSerializer.Deserialize<StreamAssist.Shell.WindowLayoutModel>(JsonSerializer.Serialize(layout))!, BlockGroup.Twitch).Groups[0].ShowCards);
            palette.SearchText = "announce";
            palette.GetType().GetMethod("ApplySearch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(palette, null);
            Check("block list: searching keeps a heading collapsed, counting its matches", !twitchGroup.ShowCards && twitchGroup.Heading == $"Twitch ({twitchGroup.Cards.Count}) ▸"
                && twitchGroup.Cards.All(c => c.Name.Contains("announce", StringComparison.OrdinalIgnoreCase)) && twitchGroup.Cards.Count > 0);
            Check("block list: a heading with no matches is hidden", !palette.Groups.Any(g => g.Group == BlockGroup.Control));
            twitchGroup.HeadingPressedCommand.Execute(null);
            Check("block list: a heading can be expanded while searching", twitchGroup.ShowCards && twitchGroup.Heading.EndsWith("▾"));
            twitchGroup.HeadingPressedCommand.Execute(null);
            palette.SearchText = "";
            palette.GetType().GetMethod("ApplySearch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(palette, null);
            twitchGroup.HeadingPressedCommand.Execute(null);
            Check("block list: clicking again expands it", twitchGroup.ShowCards && !layout.IsBlockGroupCollapsed("Twitch"));
            palette.Dispose();
            otherPalette.Dispose();
            Check("value blocks don't fit action slots", !new BlockSlotList("", typeof(ExecutableBlock)).Accepts(new StringBuilderBlock()));
        }

        // ---- any-type blocks, conversions, parameter lists ----
        {
            var ctx = new TriggerContext(TriggerPhase.Once, default, new Dictionary<string, string> { ["viewers"] = "57", ["price"] = "2.7" });

            // Convert: one block, the rules in ValueTypes.TryConvert
            Check("double -> int truncates", ValueTypes.TryConvert(2.7, ValueKind.Double, ValueKind.Int, out var t1, out _) && (int)t1 == 2
                && ValueTypes.TryConvert(-2.7, ValueKind.Double, ValueKind.Int, out var t2, out _) && (int)t2 == -2);
            Check("text '2.5' is not an int", !ValueTypes.TryConvert("2.5", ValueKind.Text, ValueKind.Int, out _, out var e1) && e1 == "'2.5' is not a whole number");
            Check("bool can't become a number", !ValueTypes.TryConvert(true, ValueKind.Bool, ValueKind.Int, out _, out var e2) && e2 == "true or false can't become a whole number");
            var convertBlock = new ConvertBlock { To = "Int" };
            ((ValueSlot)convertBlock.Slots[0]).BlockSlot.TrySetBlock(DoubleParam("price"));
            Check("Convert a double block to int", (int)await convertBlock.EvaluateObjectAsync(ctx) == 2);

            // dropped into a slot, an any-type block takes the slot's kind (if it doesn't already fit)
            var intSlot = new ValueSlot<int>("Amount");
            var param = new ConvertBlock(); // Text by default
            ((ValueSlot)param.Slots[0]).SetText("{viewers}");
            Check("int slot takes Convert (it can be an int)", intSlot.BlockSlot.Accepts(param));
            intSlot.BlockSlot.TrySetBlock(param);
            Check("...and it becomes Int there", param.Kind == ValueKind.Int && param.To == "Int" && await intSlot.EvaluateAsync(ctx) == 57);
            Check("changing it to Text is refused (the slot needs an int)", !param.TrySetKind(ValueKind.Text) && param.Kind == ValueKind.Int);
            param.To = "Text";
            Check("...through the dropdown too", param.To == "Int");
            Check("changing to Double is refused too (double doesn't fit int)", !param.TrySetKind(ValueKind.Double));

            var textSlot = new ValueSlot<string>("Message");
            var intParam = IntParam("viewers");
            textSlot.BlockSlot.TrySetBlock(intParam);
            Check("an int Convert already fits a text slot, so it stays Int", intParam.Kind == ValueKind.Int);
            Check("...and can change to anything, text takes all", intParam.TrySetKind(ValueKind.Bool) && intParam.TrySetKind(ValueKind.Int));
            Check("Convert can be a char", new ConvertBlock().CanBe(ValueKind.Char));

            var freeParam = new ConvertBlock();
            Check("not placed: any allowed kind", freeParam.TrySetKind(ValueKind.Double) && freeParam.Kind == ValueKind.Double);

            // condition slots take any bool value block
            var ifCondition = new BlockSlot("", typeof(ConditionBlock));
            var convertToBool = MakeConvert("Text", "true");
            Check("condition slot takes Convert (it can be Bool)", ifCondition.Accepts(convertToBool));
            ifCondition.TrySetBlock(convertToBool);
            Check("...becomes Bool and evaluates", convertToBool.Kind == ValueKind.Bool && await ifCondition.EvaluateAsync(ctx));
            Check("condition slot refuses String Builder (text)", !ifCondition.Accepts(new StringBuilderBlock()));
            var and = new AndCondition();
            ((BlockSlotList)and.Slots[0]).TryInsertBlock(MakeConvert("Bool", "true"), 0);
            ((BlockSlotList)and.Slots[0]).TryInsertBlock(new FixedCondition { Value = true }, 1);
            Check("And mixes conditions and bool value blocks", await and.EvaluateAsync(ctx));
            var branches = new BlockBranchList("");
            Check("If branches accept bool value blocks", branches.TryInsertBranch(MakeConvert("Text", "false"), 0) && branches.Branches[0].Condition.Block!.ParentSlot == branches.Branches[0].Condition);

            // the slot a block is in
            var slotJoin = new StringBuilderBlock();
            var holder = new ValueSlot<string>("x"); holder.BlockSlot.TrySetBlock(slotJoin);
            Check("a placed block knows its slot", slotJoin.ParentSlot == holder.BlockSlot);
            holder.BlockSlot.RemoveBlock(slotJoin);
            Check("...and forgets it when removed", slotJoin.ParentSlot == null);

            // saving keeps the chosen kind (and loading doesn't re-adapt it)
            var savedConvert = new ConsoleMessageAction();
            var innerConvert = new ConvertBlock { To = "Double" };
            ((ValueSlot)savedConvert.Slots[0]).BlockSlot.TrySetBlock(innerConvert);
            var convertJson = JsonSerializer.Serialize(BlockResolver.GetData(savedConvert));
            var reloaded = BlockResolver.Resolve(JsonSerializer.Deserialize<PendingBlockData>(convertJson)!)!;
            var reloadedConvert = (ConvertBlock)((ValueSlot)reloaded.Slots[0]).BlockSlot.Block!;
            Check("Convert's kind is saved", convertJson.Contains("\"kind\":\"Double\"") && reloadedConvert.Kind == ValueKind.Double);

            // parameter lists come from the same data the app uses
            var chatEvent = EventSubMessage.Parse(
                "{\"metadata\":{\"message_id\":\"x\",\"message_type\":\"notification\",\"message_timestamp\":\"2026-10-28T18:30:00Z\",\"subscription_type\":\"channel.chat.message\",\"subscription_version\":\"1\"},"
                + "\"payload\":{\"subscription\":{\"id\":\"s\"},\"event\":{\"broadcaster_user_id\":\"1\",\"broadcaster_user_login\":\"s\",\"broadcaster_user_name\":\"S\",\"chatter_user_id\":\"2\",\"chatter_user_login\":\"v\",\"chatter_user_name\":\"V\",\"message_id\":\"m\",\"message\":{\"text\":\"hi\"},\"color\":\"\",\"badges\":[]}}}").Event!;
            Check("chat parameter list matches what chat messages give", chatEvent.ChatParameters().Keys.OrderBy(k => k).SequenceEqual(ChatEvents.Chat.Select(p => p.Name).OrderBy(k => k)));
            Check("chat test panel gives the same parameters", ChatEvents.SampleChatParameters("U", "!x", true, false, true).Keys.OrderBy(k => k).SequenceEqual(ChatEvents.Chat.Select(p => p.Name).OrderBy(k => k)));
            foreach (var (key, name) in ChatEvents.All)
            {
                var listed = ChatEvents.Parameters(key).Select(p => p.Name).ToHashSet();
                Check($"{name}: test values only use listed parameters", ChatEvents.SampleParameters(key, "U", "5", "1", "t").Keys.All(listed.Contains));
            }
            Check("redeem test values only use listed parameters", ChatEvents.SampleParameters(null, "U", "5", "1", "t", "R").Keys.All(ChatEvents.Redeem.Select(p => p.Name).Contains));

            // what dispatched twitch events give is listed too
            var model = Chatbot();
            var seenKeys = new Dictionary<string, HashSet<string>>();
            foreach (var (key, _) in ChatEvents.All)
            {
                var slot = model.Events.First(e => e.Key == key);
                var keysBinding = new BindableModel(); keysBinding.Root.TrySetBlock(new KeysRecordBlock()); slot.Target.TryBind(keysBinding);
            }
            TwitchEvent Ev(string type, string evJson) => EventSubMessage.Parse(
                "{\"metadata\":{\"message_id\":\"x\",\"message_type\":\"notification\",\"message_timestamp\":\"t\",\"subscription_type\":\"" + type + "\",\"subscription_version\":\"1\"},"
                + "\"payload\":{\"subscription\":{\"id\":\"s\",\"type\":\"" + type + "\"},\"event\":" + evJson + "}}").Event!;
            async Task<string[]> KeysFor(string type, string json) { KeysRecordBlock.Seen = null; ChatEventManager.Dispatch(model, Ev(type, json)); await Task.Delay(50); return KeysRecordBlock.Seen ?? []; }
            var raidKeys = await KeysFor("channel.raid", """{"from_broadcaster_user_name":"T","from_broadcaster_user_login":"t","from_broadcaster_user_id":"1","to_broadcaster_user_name":"Me","viewers":9}""");
            Check("raid gives only listed parameters", raidKeys.Length > 0 && raidKeys.All(ChatEvents.Parameters(ChatEvents.Raid).Select(p => p.Name).Contains));
            var subKeys = await KeysFor("channel.subscription.message", """{"user_name":"V","tier":"2000","cumulative_months":15,"streak_months":3,"message":{"text":"hi"}}""");
            Check("resub gives only listed parameters", subKeys.Length > 0 && subKeys.All(ChatEvents.Parameters(ChatEvents.Sub).Select(p => p.Name).Contains));
            var giftKeys = await KeysFor("channel.subscription.gift", """{"user_name":"G","total":5,"tier":"1000","is_anonymous":false,"cumulative_total":9}""");
            Check("gift gives only listed parameters", giftKeys.Length > 0 && giftKeys.All(ChatEvents.Parameters(ChatEvents.GiftSubs).Select(p => p.Name).Contains));
            var cheerKeys = await KeysFor("channel.cheer", """{"user_name":"C","bits":100,"message":"x","is_anonymous":false}""");
            Check("cheer gives only listed parameters", cheerKeys.Length > 0 && cheerKeys.All(ChatEvents.Parameters(ChatEvents.Cheer).Select(p => p.Name).Contains));

            // a chat command's list includes what its path captures
            var tree = new MatchTextChatBlock();
            tree.Rows.Rows[0].SetPattern("/!give (?<amount>\\d+) /");
            var who = new ParameterChatBlock { ParameterName = "target" };
            tree.Rows.Rows[0].Target.Child.TrySetBlock(who);
            var bindBox = who.Target;
            model.Root.TrySetBlock(tree);
            var chatParams = model.FindParameters(bindBox)!.Select(p => p.Name).ToList();
            Check("chat command parameters: captures + chat + rest", chatParams.Contains("amount") && chatParams.Contains("target") && chatParams.Contains("user") && chatParams.Contains("rest"));
            Check("raid binding parameters", model.FindParameters(model.Events.First(e => e.Key == ChatEvents.Raid).Target)!.Any(p => p.Name == "viewers" && p.Kind == ValueKind.Int));
            Check("redeem binding parameters", model.FindParameters(model.Redeems.Rows[0].Target)!.Any(p => p.Name == "cost"));
            Check("a target outside the chatbot has none", model.FindParameters(new ChatTarget()) == null);
        }

        // ---- twitch action queue + moderation blocks ----
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
        {
            var cgj = " " + (char)0x034F;
            var first = ChatbotManager.AvoidDuplicate("yo", null);
            var second = ChatbotManager.AvoidDuplicate("yo", first);
            var third = ChatbotManager.AvoidDuplicate("yo", second);
            Check("repeats alternate: yo, yo + space + CGJ, yo", first == "yo" && second == "yo" + cgj && third == "yo");
            Check("a different message is left alone", ChatbotManager.AvoidDuplicate("hi", "yo") == "hi");
            Check("spaces around don't make it different", ChatbotManager.AvoidDuplicate("yo ", "yo") == "yo " + cgj);
        }


        // ---- loops, wait, stop, random, text (runs go on a worker thread: on the UI thread a loop pauses
        // through the dispatcher, which nothing runs here. Failures are checked by the run stopping, not the console) ----
        {
            T Slotted<T>(T block, params string[] slotTexts) where T : BlockModel
            {
                for (int i = 0; i < slotTexts.Length; i++) if (slotTexts[i] != null) ((ValueSlot)block.Slots[i]).SetText(slotTexts[i]);
                return block;
            }
            T Holding<T>(T block, int slot, BlockModel child) where T : BlockModel { ((BlockSlot)block.Slots[slot]).TrySetBlock(child); return block; }
            ListBlock Steps(params BlockModel[] blocks)
            {
                var list = new ListBlock();
                for (int i = 0; i < blocks.Length; i++) ((BlockSlotList)list.Slots[0]).TryInsertBlock(blocks[i], i);
                return list;
            }
            FormatRecordBlock Say(string template) => new() { Template = template };
            async Task<string> RunTree(ExecutableBlock root, CancellationToken ct = default)
            {
                lock (FormatRecordBlock.Log) FormatRecordBlock.Log.Clear();
                await Task.Run(() => root.SafeRunAsync(new TriggerContext(TriggerPhase.Once, ct, new Dictionary<string, string> { ["message"] = "!so  budder" })));
                lock (FormatRecordBlock.Log) return string.Join(",", FormatRecordBlock.Log);
            }
            async Task<object> Value(ValueBlock block) => await block.EvaluateObjectAsync(new TriggerContext(TriggerPhase.Once, default));
            async Task<bool> Fails(ValueBlock block) { try { await Value(block); return false; } catch (BlockFailedException) { return true; } }

            // repeat
            Check("repeat: runs the block inside that many times, {i} counts from 1", await RunTree(Holding(Slotted(new RepeatBlock(), "3"), 1, Say("{i}"))) == "1,2,3");
            Check("repeat: Number As names the count", await RunTree(Holding(Slotted(new RepeatBlock { NumberName = "round" }, "2"), 1, Say("r{round}"))) == "r1,r2");
            Check("repeat: 0 times runs nothing", await RunTree(Steps(Holding(Slotted(new RepeatBlock(), "0"), 1, Say("x")), Say("after"))) == "after");
            Check("repeat: a negative count fails and stops the run", await RunTree(Steps(Holding(Slotted(new RepeatBlock(), "-1"), 1, Say("x")), Say("after"))) == "");
            Check("repeat: a bad Number As name fails", await RunTree(Steps(Holding(Slotted(new RepeatBlock { NumberName = "my count" }, "2"), 1, Say("x")), Say("after"))) == "");

            // for each
            Check("for each: each part of a list, trimmed, empty ones skipped", await RunTree(Holding(Slotted(new ForEachBlock(), "Cam, Title, , Mic"), 2, Say("{item}"))) == "Cam,Title,Mic");
            Check("for each: an empty separator means words", await RunTree(Holding(Slotted(new ForEachBlock { ItemName = "word" }, "{message}", ""), 2, Say("[{word}]"))) == "[!so],[budder]");
            Check("for each: an empty list runs nothing", await RunTree(Steps(Holding(Slotted(new ForEachBlock(), ""), 2, Say("x")), Say("after"))) == "after");

            // while
            // is empty
            Check("is empty: empty or only spaces", await Value(Slotted(new IsEmptyCondition(), "")) is true && await Value(Slotted(new IsEmptyCondition(), "  ")) is true
                && await Value(Slotted(new IsEmptyCondition(), " x ")) is false && new IsEmptyCondition().Group == BlockGroup.Text);
            Check("equals: no A / B labels, == shown between the values", ((ValueSlot)new EqualsCondition().Slots[1]).Connector == "==" && ((ValueSlot)new EqualsCondition().Slots[0]).Label == "");

            Check("while: runs while the condition is true", await RunTree(Steps(Holding(Holding(new WhileBlock(), 0, new CountdownCondition { Left = 3 }), 1, Say("x")), Say("after"))) == "x,x,x,after");
            Check("while: an empty condition is false", await RunTree(Steps(Holding(new WhileBlock(), 1, Say("x")), Say("after"))) == "after");
            var forever = Holding(new WhileBlock(), 0, new FixedCondition { Value = true });
            var foreverWatch = System.Diagnostics.Stopwatch.StartNew();
            Check("while: a loop that never waits is stopped (and the run with it)", await RunTree(Steps(forever, Say("after"))) == "" && foreverWatch.Elapsed < TimeSpan.FromSeconds(20));
            using (var stopLoop = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)))
            {
                var waitingLoop = Holding(Holding(new WhileBlock(), 0, new FixedCondition { Value = true }), 1, Steps(Slotted(new WaitAction(), "0.01"), Say("tick")));
                var ticks = (await RunTree(waitingLoop, stopLoop.Token)).Split(',', StringSplitOptions.RemoveEmptyEntries).Length;
                Check("while: cancelling the run (binding removed, app closed) stops a loop", ticks is > 0 and < 50);
            }

            // wait
            var waitWatch = System.Diagnostics.Stopwatch.StartNew();
            Check("wait: waits before the next block", await RunTree(Steps(Slotted(new WaitAction(), "0.1"), Say("after"))) == "after" && waitWatch.ElapsedMilliseconds >= 90);
            using (var stopWait = new CancellationTokenSource(TimeSpan.FromMilliseconds(50)))
            {
                var cancelWatch = System.Diagnostics.Stopwatch.StartNew();
                Check("wait: cancelling stops it straight away", await RunTree(Steps(Slotted(new WaitAction(), "10"), Say("after")), stopWait.Token) == "" && cancelWatch.Elapsed < TimeSpan.FromSeconds(5));
            }
            Check("wait: a negative time fails", await RunTree(Steps(Slotted(new WaitAction(), "-1"), Say("after"))) == "");

            // stop
            Check("stop: ends the run, quietly", await RunTree(Steps(Say("a"), new StopAction(), Say("b"))) == "a");
            Check("stop: inside a loop ends the whole run", await RunTree(Steps(Holding(Slotted(new RepeatBlock(), "5"), 1, Steps(Say("{i}"), new StopAction())), Say("after"))) == "1");

            // random values
            var unit = new List<double>();
            for (int i = 0; i < 200; i++) unit.Add((double)await Value(new RandomNumberBlock()));
            Check("random number: a decimal from 0 to 1 by default", unit.All(n => n is >= 0 and <= 1) && unit.Distinct().Count() > 150 && unit.Any(n => n < 0.5) && unit.Any(n => n > 0.5));
            var dice = new HashSet<object>();
            var die = new RandomNumberBlock();
            die.TrySetKind(ValueKind.Int);
            Slotted(die, "1", "6");
            for (int i = 0; i < 400; i++) dice.Add(await Value(die));
            Check("random number: in a whole number slot both ends come up (a dice roll)", dice.SetEquals(new object[] { 1, 2, 3, 4, 5, 6 }));
            var decimal1 = (double)await Value(Slotted(new RandomNumberBlock(), "2", "1"));
            Check("random number: decimals in between, Min and Max either way round", decimal1 is >= 1 and <= 2 && decimal1 != Math.Truncate(decimal1));
            var noWhole = new RandomNumberBlock();
            noWhole.TrySetKind(ValueKind.Int);
            Check("random number: no whole number in range fails", await Fails(Slotted(noWhole, "1.2", "1.8")));
            var chances = new List<bool>();
            for (int i = 0; i < 200; i++) chances.Add((bool)await Value(new ChanceCondition()));
            Check("chance: 50% is sometimes true, sometimes false", chances.Contains(true) && chances.Contains(false));
            Check("chance: 0 is never true, 100 always", !(bool)await Value(Slotted(new ChanceCondition(), "0")) && (bool)await Value(Slotted(new ChanceCondition(), "100"))
                && (bool)await Value(Slotted(new ChanceCondition(), "250")));
            var replies = new PickRandomBlock();
            replies.Choices.Items[^1].SetText("hi");
            replies.Choices.Items[^1].SetText("hey");
            var said = new HashSet<object>();
            for (int i = 0; i < 60; i++) said.Add(await Value(replies));
            Check("pick random: picks among the filled texts", replies.Choices.Items.Count == 3 && said.SetEquals(new object[] { "hi", "hey" }));
            Check("pick random: nothing to pick fails", await Fails(new PickRandomBlock()));

            // text
            Check("text contains: contains / starts / ends, case ignored", (bool)await Value(Slotted(new TextContainsCondition(), "Hello World", "WORLD"))
                && (bool)await Value(Slotted(new TextContainsCondition { Check = "Starts With" }, "!lurk now", "!LURK"))
                && !(bool)await Value(Slotted(new TextContainsCondition { Check = "Ends With" }, "!lurk now", "lurk")));
            Check("text contains: match case", !(bool)await Value(Slotted(new TextContainsCondition { Case = "Match Case" }, "Hello", "hello")));
            Check("change text: upper / lower / trim / capitalize", (string)await Value(Slotted(new ChangeTextBlock(), "Hi There")) == "HI THERE"
                && (string)await Value(Slotted(new ChangeTextBlock { Change = "Lower Case" }, "Hi There")) == "hi there"
                && (string)await Value(Slotted(new ChangeTextBlock { Change = "Trim" }, "  hi  ")) == "hi"
                && (string)await Value(Slotted(new ChangeTextBlock { Change = "Capitalize" }, "budder")) == "Budder");
            Check("text length: characters and words", (int)await Value(Slotted(new TextLengthBlock(), "hello")) == 5
                && (int)await Value(Slotted(new TextLengthBlock { Count = "Words" }, "  hi   there you ")) == 3);
            Check("replace text: every one, case ignored unless Match Case", (string)await Value(Slotted(new ReplaceTextBlock(), "Hello hello", "HELLO", "bye")) == "bye bye"
                && (string)await Value(Slotted(new ReplaceTextBlock { Case = "Match Case" }, "Hello hello", "hello", "")) == "Hello ");
            Check("replace text: an empty Find fails", await Fails(Slotted(new ReplaceTextBlock(), "x", "", "y")));
            Check("get part: words, from the end, past the end", (string)await Value(Slotted(new GetPartBlock(), "!so  budder", "", "2")) == "budder"
                && (string)await Value(Slotted(new GetPartBlock(), "a b c", "", "-1")) == "c" && (string)await Value(Slotted(new GetPartBlock(), "a b", "", "5")) == "");
            Check("get part: a list separator", (string)await Value(Slotted(new GetPartBlock(), "Cam, Title, Mic", ", ", "2")) == "Title");
            Check("get part: number 0 fails", await Fails(Slotted(new GetPartBlock(), "a", "", "0")));

            // saving, and the block list
            var savedLoop = (RepeatBlock)BlockResolver.DeepCopy(Holding(Slotted(new RepeatBlock { NumberName = "n" }, "{times}"), 1, Say("{n}")))!;
            Check("logic blocks: saved and loaded keep their settings and children", savedLoop.NumberName == "n" && ((ValueSlot)savedLoop.Slots[0]).Text == "{times}"
                && ((BlockSlot)savedLoop.Slots[1]).Block is FormatRecordBlock { Template: "{n}" }
                && BlockResolver.DeepCopy(new TextContainsCondition { Check = "Ends With", Case = "Match Case" }) is TextContainsCondition { Check: "Ends With", Case: "Match Case" });
            var listed = BlockResolver.DiscoverAll().ToDictionary(b => b.GetType(), b => b.Group);
            Check("logic blocks: in the block list under their headings", listed[typeof(RepeatBlock)] == BlockGroup.Control && listed[typeof(WhileBlock)] == BlockGroup.Control
                && listed[typeof(ForEachBlock)] == BlockGroup.Control && listed[typeof(WaitAction)] == BlockGroup.Control && listed[typeof(StopAction)] == BlockGroup.Control
                && listed[typeof(ChanceCondition)] == BlockGroup.Logic && listed[typeof(TextContainsCondition)] == BlockGroup.Text
                && listed[typeof(RandomNumberBlock)] == BlockGroup.Math && listed[typeof(PickRandomBlock)] == BlockGroup.Text
                && listed[typeof(TextLengthBlock)] == BlockGroup.Text && listed[typeof(GetPartBlock)] == BlockGroup.Text);
        }

        // ---- config files: a broken one is never overwritten (in a temporary folder, never the real Config/) ----
        {
            var realRoot = StreamAssist.Core.Persistence.JsonFileStore.RootDir;
            var (realError, realWarning) = (StreamAssist.Core.Persistence.JsonFileStore.ReportError, StreamAssist.Core.Persistence.JsonFileStore.ReportWarning);
            var dir = Path.Combine(Path.GetTempPath(), "StreamAssistTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var reported = new List<string>();
            var changed = new List<string>();
            void OnBroken(string name) => changed.Add(name);
            StreamAssist.Core.Persistence.JsonFileStore.RootDir = dir;
            StreamAssist.Core.Persistence.JsonFileStore.ReportError = m => reported.Add("error - " + m);
            StreamAssist.Core.Persistence.JsonFileStore.ReportWarning = m => reported.Add("warning - " + m);
            StreamAssist.Core.Persistence.JsonFileStore.BrokenChanged += OnBroken;
            try
            {
                Check("files: a missing file loads as defaults, quietly", StreamAssist.Core.Persistence.JsonFileStore.Load<Dictionary<string, int>>("fresh").Count == 0 && reported.Count == 0);
                StreamAssist.Core.Persistence.JsonFileStore.Save("fresh", new Dictionary<string, int> { ["a"] = 1 });
                Check("files: saved and loaded, no temporary file left", StreamAssist.Core.Persistence.JsonFileStore.Load<Dictionary<string, int>>("fresh")["a"] == 1
                    && !File.Exists(Path.Combine(dir, "fresh.json.tmp")));

                File.WriteAllText(Path.Combine(dir, "keys.json"), "{\n  \"a\": 1,\n  \"b\": oops\n}");
                var keys = StreamAssist.Core.Persistence.JsonFileStore.Load<Dictionary<string, int>>("keys");
                Check("files: a broken file loads as defaults, marked broken, one error saying where", keys.Count == 0 && StreamAssist.Core.Persistence.JsonFileStore.IsBroken("keys")
                    && reported.Count == 1 && reported[0].StartsWith("error - keys.json can't be read, not saving it (line 3") && changed.SequenceEqual(new[] { "keys" }));
                StreamAssist.Core.Persistence.JsonFileStore.Save("keys", new Dictionary<string, int> { ["new"] = 2 });
                StreamAssist.Core.Persistence.JsonFileStore.Load<Dictionary<string, int>>("keys");
                Check("files: a broken file is never saved over, and reported once", File.ReadAllText(Path.Combine(dir, "keys.json")).Contains("oops") && reported.Count == 1);
                Check("files: back up & start fresh renames it and saving carries on", StreamAssist.Core.Persistence.JsonFileStore.TryBackUpAndStartFresh("keys")
                    && !StreamAssist.Core.Persistence.JsonFileStore.IsBroken("keys") && changed.SequenceEqual(new[] { "keys", "keys" })
                    && Directory.GetFiles(dir, "keys.broken-*.json").Single() is { } backup && File.ReadAllText(backup).Contains("oops")
                    && new Func<bool>(() => { StreamAssist.Core.Persistence.JsonFileStore.Save("keys", new Dictionary<string, int> { ["new"] = 2 }); return StreamAssist.Core.Persistence.JsonFileStore.Load<Dictionary<string, int>>("keys")["new"] == 2; })());

                File.WriteAllText(Path.Combine(dir, "small.json"), "not json");
                var small = StreamAssist.Core.Persistence.JsonFileStore.LoadOrReset<Dictionary<string, int>>("small");
                Check("files: a broken small settings file is backed up with a warning and reset", small.Count == 0 && !StreamAssist.Core.Persistence.JsonFileStore.IsBroken("small")
                    && reported.Last().StartsWith("warning - small.json can't be read, backed up as small.broken-") && Directory.GetFiles(dir, "small.broken-*.json").Length == 1
                    && !File.Exists(Path.Combine(dir, "small.json")));

                File.WriteAllText(Path.Combine(dir, "auth.json"), "{\"twitch\": {\"id\": \"keep me\"}, broken");
                Check("files: an unreadable auth.json isn't readable", !StreamAssist.Core.Persistence.CredentialStore.IsReadable);
                StreamAssist.Core.Persistence.CredentialStore.Save("spotifyLogin", new Dictionary<string, string> { ["token"] = "x" });
                Check("files: saving one login can't wipe the others in a broken auth.json", File.ReadAllText(Path.Combine(dir, "auth.json")).Contains("keep me"));
                StreamAssist.Core.Persistence.JsonFileStore.TryBackUpAndStartFresh("auth");
                StreamAssist.Core.Persistence.CredentialStore.Save("spotifyLogin", new Dictionary<string, string> { ["token"] = "x" });
                Check("files: after backing it up, logins save again", StreamAssist.Core.Persistence.CredentialStore.IsReadable
                    && StreamAssist.Core.Persistence.CredentialStore.Load<Dictionary<string, string>>("spotifyLogin")["token"] == "x");
            }
            finally
            {
                StreamAssist.Core.Persistence.JsonFileStore.BrokenChanged -= OnBroken;
                StreamAssist.Core.Persistence.JsonFileStore.RootDir = realRoot;
                StreamAssist.Core.Persistence.JsonFileStore.ReportError = realError;
                StreamAssist.Core.Persistence.JsonFileStore.ReportWarning = realWarning;
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }

        // ---- data: tables, variables and the SQLite store (a temporary database, never the real Config/data.db) ----
        {
            var dataDir = Path.Combine(Path.GetTempPath(), "StreamAssistData-" + Guid.NewGuid().ToString("N"));
            var dbPath = Path.Combine(dataDir, "data.db");
            var data = new DataModel();
            var dataStore = new DataStore(data, dbPath);

            Check("data: a new database has the built-in variables table", data.FindTable("variables") is { IsBuiltIn: true } vars
                && vars.Columns.Select(c => c.Name).SequenceEqual(new[] { "name", "type", "value" }));
            Check("data: a built-in table keeps its columns, its name and itself", !data.TryAddColumn("variables", "x", ValueKind.Text) && !data.TryDeleteColumn("variables", "value")
                && !data.TryRenameColumn("variables", "value", "v") && !data.TryDeleteTable("variables") && !data.TryRenameTable("variables", "vars"));

            // variables
            data.TryAddRow("variables", out var deaths);
            Check("data: a new variable gets a free name, as text", deaths.Get("name") is "variable1" && data.TryGetVariable("variable1", out var newKind, out var newValue) && newKind == ValueKind.Text && newValue is "");
            Check("data: making a variable a whole number gives it 0", data.TrySetValue("variables", deaths.Id, "name", "deaths") && data.TrySetValue("variables", deaths.Id, "type", "Int")
                && data.TryGetVariable("DEATHS", out var deathsKind, out var deathsValue) && deathsKind == ValueKind.Int && deathsValue is 0);
            Check("data: a variable's value has to fit its type", !data.TrySetValue("variables", deaths.Id, "value", "abc") && data.TrySetValue("variables", deaths.Id, "value", " 12 ")
                && deaths.Get("value") is "12");
            data.TryAddRow("variables", out var other);
            Check("data: variable names are valid and unique", !data.TrySetValue("variables", other.Id, "name", "Deaths") && !data.TrySetValue("variables", other.Id, "name", "my var")
                && data.TrySetValue("variables", other.Id, "name", "greeting") && data.TrySetValue("variables", other.Id, "value", "hi"));
            Check("data: a type its value doesn't fit is refused", !data.TrySetValue("variables", other.Id, "type", "Int") && other.Get("type") is "Text");
            Check("data: change adds to numbers, text can't", data.TryChangeVariable("deaths", 1, out var plusOne) && plusOne is 13 && data.TryChangeVariable("deaths", -3, out _)
                && !data.TryChangeVariable("greeting", 1, out _) && !data.TryChangeVariable("deaths", 0.5, out _));
            Check("data: set converts to the variable's type", data.TrySetVariable("deaths", 10.0, ValueKind.Double) && data.TryGetVariable("deaths", out _, out var ten) && ten is 10
                && !data.TrySetVariable("deaths", "lots"));
            data.TryAddRow("variables", out var flag);
            data.TrySetValue("variables", flag.Id, "name", "live");
            data.TrySetValue("variables", flag.Id, "type", "Bool");
            Check("data: change flips true/false", data.TryChangeVariable("live", 1, out var flipped) && flipped is true && data.VariableNames().SequenceEqual(new[] { "deaths", "greeting", "live" }));

            // custom tables
            Check("data: table and column names are checked", !data.TryCreateTable("my table") && !data.TryCreateTable("1st") && data.TryCreateTable("quotes") && !data.TryCreateTable("QUOTES")
                && data.TryAddColumn("quotes", "text", ValueKind.Text) && data.TryAddColumn("quotes", "year", ValueKind.Int) && !data.TryAddColumn("quotes", "id", ValueKind.Int)
                && !data.TryAddColumn("quotes", "Text", ValueKind.Int));
            data.TryAddRow("quotes", out var q1);
            data.TryAddRow("quotes", out var q2);
            Check("data: rows get ids 1, 2, ... and empty cells", q1.Id == 1 && q2.Id == 2 && q1.Get("text") is "" && q1.Get("year") == null);
            Check("data: a cell has to fit its column", data.TrySetValue("quotes", 1, "text", "hello there") && data.TrySetValue("quotes", 1, "year", "2024") && q1.Get("year") is 2024
                && !data.TrySetValue("quotes", 1, "year", "soon") && q1.Get("year") is 2024 && data.TrySetValue("quotes", 1, "year", "") && q1.Get("year") == null);
            data.TrySetValue("quotes", 1, "year", 2024, ValueKind.Int);
            Check("data: a column's type changes only if every value fits", data.TrySetColumnKind("quotes", "year", ValueKind.Text) && q1.Get("year") is "2024"
                && data.TrySetValue("quotes", 2, "year", "someday") && !data.TrySetColumnKind("quotes", "year", ValueKind.Int) && data.TrySetValue("quotes", 2, "year", "1999")
                && data.TrySetColumnKind("quotes", "year", ValueKind.Int) && q2.Get("year") is 1999);
            Check("data: change value adds to a number cell", data.TryChangeValue("quotes", 2, "year", 1) && q2.Get("year") is 2000 && !data.TryChangeValue("quotes", 2, "text", 1));
            Check("data: columns and tables rename", data.TryRenameColumn("quotes", "text", "quote") && q1.Get("quote") is "hello there" && data.TryRenameTable("quotes", "sayings")
                && data.FindTable("quotes") == null && data.TryRenameTable("sayings", "quotes"));
            data.TryCreateTable("scratch");
            data.TryAddColumn("scratch", "x", ValueKind.Double);
            Check("data: delete a column, a row and a table", data.TryDeleteColumn("scratch", "x") && data.FindTable("scratch")!.Columns.Count == 0
                && data.TryAddRow("scratch", out var gone) && data.TryDeleteRow("scratch", gone.Id) && !data.TryDeleteRow("scratch", gone.Id) && data.TryDeleteTable("scratch"));

            // renumbering closes the gaps deleted rows left, in order, and the next row comes after
            data.TryCreateTable("gaps");
            data.TryAddColumn("gaps", "v", ValueKind.Text);
            for (int i = 1; i <= 4; i++) { data.TryAddRow("gaps", out var g); data.TrySetValue("gaps", g.Id, "v", $"r{i}"); }
            data.TryDeleteRow("gaps", 1); data.TryDeleteRow("gaps", 3);
            Check("data: renumber rows", data.TryRenumberRows("gaps") && data.FindTable("gaps")!.Rows.Select(r => $"{r.Id}:{r.Get("v")}").SequenceEqual(new[] { "1:r2", "2:r4" })
                && data.TryAddRow("gaps", out var afterGaps) && afterGaps.Id == 3);
            // saved: a new model loading the same database has everything
            dataStore.Dispose();
            var reloaded = new DataModel();
            var reloadedStore = new DataStore(reloaded, dbPath);
            var quotes = reloaded.FindTable("quotes");
            Check("data: variables are kept in the database", reloaded.TryGetVariable("deaths", out var keptKind, out var kept) && keptKind == ValueKind.Int && kept is 10
                && reloaded.TryGetVariable("live", out _, out var keptFlag) && keptFlag is true && reloaded.FindTable("variables")!.IsBuiltIn);
            Check("data: tables, columns, types, ids and values are kept", quotes != null && !quotes.IsBuiltIn && quotes.Columns.Select(c => $"{c.Name}:{c.Kind}").SequenceEqual(new[] { "quote:Text", "year:Int" })
                && quotes.Rows.Select(r => r.Id).SequenceEqual(new[] { 1, 2 }) && quotes.FindRow(1)!.Get("quote") is "hello there" && quotes.FindRow(2)!.Get("year") is 2000
                && reloaded.FindTable("scratch") == null && reloaded.TryAddRow("quotes", out var q3) && q3.Id == 3);
            reloaded.TryDeleteRow("quotes", 3);
            Check("data: renumbered ids are kept in the database", reloaded.FindTable("gaps")!.Rows.Select(r => $"{r.Id}:{r.Get("v")}").SequenceEqual(new[] { "1:r2", "2:r4", "3:" }));

            // blocks
            StreamAssist.Shell.GlobalContext.DataModel = reloaded;
            TriggerContext.VariableText = name => reloaded.VariableKind(name) != null && reloaded.TryGetVariable(name, out _, out var v) ? ValueTypes.ToText(v) : null;
            var dctx = new TriggerContext(TriggerPhase.Once, default, new Dictionary<string, string> { ["userLogin"] = "budder" });
            async Task<bool> Do(ExecutableBlock block) { RecordBlock.Log.Clear(); var list = new ListBlock(); ((BlockSlotList)list.Slots[0]).TryInsertBlock(block, 0); ((BlockSlotList)list.Slots[0]).TryInsertBlock(new RecordBlock { Tag = "after" }, 1); await list.SafeRunAsync(dctx); return RecordBlock.Log.Count == 1; }
            // types into the block's slots after its Table slot (the table is set by name with Table = ...)
            T With<T>(T block, params string[] texts) where T : BlockModel
            {
                var slots = block.Slots.OfType<ValueSlot>().Where(v => v.Label != "Table").ToList();
                for (int i = 0; i < texts.Length; i++) slots[i].SetText(texts[i]);
                return block;
            }

            Check("data blocks: set variable", await Do(With(new SetVariableAction { Variable = "deaths" }, "5")) && reloaded.TryGetVariable("deaths", out _, out var five) && five is 5);
            Check("data blocks: a value that doesn't fit fails and stops", !await Do(With(new SetVariableAction { Variable = "deaths" }, "lots")));
            Check("data blocks: change variable", await Do(new ChangeVariableAction { Variable = "deaths" }) && reloaded.TryGetVariable("deaths", out _, out var six) && six is 6);
            var getDeaths = new GetVariableBlock { Variable = "deaths" };
            Check("data blocks: get variable picks the variable's type", getDeaths.Kind == ValueKind.Int && await getDeaths.EvaluateObjectAsync(dctx) is 6);
            Check("data blocks: an unknown variable fails", !await Do(new ChangeVariableAction { Variable = "nope" }));
            Check("data blocks: {var.name} in typed text", dctx.Format("deaths: {var.deaths}, {var.nope}, {userLogin}") == "deaths: 6, {var.nope}, budder");
            var scoped = new TriggerContext(TriggerPhase.Once, default, new Dictionary<string, string> { ["deaths"] = "run" });
            var scopeBuilder = new StringBuilderBlock();
            scopeBuilder.Text.SetText("{deaths}");
            var scopeRow = scopeBuilder.Values.Items[^1];
            scopeRow.Key.SetText("deaths");
            scopeRow.Value.SetText("block");
            Check("scopes: {name} is a variable when nothing nearer has it", dctx.Format("{deaths} deaths, {nope}") == "6 deaths, {nope}");
            Check("scopes: a run parameter wins over a variable, {var.name} still reads the variable", scoped.Format("{deaths} / {var.deaths}") == "run / 6");
            Check("scopes: a String Builder row wins over both", await scopeBuilder.EvaluateAsync(scoped) == "block" && await scopeBuilder.EvaluateAsync(dctx) == "block");
            scopeBuilder.Text.SetText("{deaths} {loc.deaths} {run.deaths} {var.deaths} {run.nope}");
            Check("scopes: loc. run. var. read only that scope", await scopeBuilder.EvaluateAsync(scoped) == "block block run 6 {run.nope}"
                && scoped.Parameters.ContainsKey("deaths") && scoped.Locals.Count == 0);

            Check("data blocks: add table row puts its id in {rowId}", await Do(new AddTableRowAction { Table = "quotes" }) && dctx.Parameters["rowId"] == "4");
            var setOne = new SetTableRowAction { Table = "quotes" };
            var oneRow = setOne.Values.Items[^1];
            oneRow.Key.SetText("quote");
            oneRow.Value.SetText("{userLogin} was here");
            Check("data blocks: set table row, one column", await Do(setOne) && reloaded.FindTable("quotes")!.FindRow(4)!.Get("quote") is "budder was here");
            Check("data blocks: change table value", await Do(With(new ChangeTableValueAction { Table = "quotes" }, "4", "year", "3")) && reloaded.FindTable("quotes")!.FindRow(4)!.Get("year") is 3);
            Check("data blocks: get table value as a type, empty reads as nothing", await With(new GetTableValueBlock { Table = "quotes", As = "Int" }, "4", "year").EvaluateObjectAsync(dctx) is 3
                && await With(new GetTableValueBlock { Table = "quotes" }, "4", "quote").EvaluateObjectAsync(dctx) is "budder was here");
            Check("data blocks: find table row (case ignored), 0 if none", await With(new FindTableRowBlock { Table = "quotes" }, "quote", "BUDDER WAS HERE").EvaluateObjectAsync(dctx) is 4
                && await With(new FindTableRowBlock { Table = "quotes" }, "quote", "nobody").EvaluateObjectAsync(dctx) is 0);
            var randomRows = new HashSet<object>();
            for (int i = 0; i < 60; i++) randomRows.Add(await new RandomTableRowBlock { Table = "quotes" }.EvaluateObjectAsync(dctx));
            Check("data blocks: random row and count", randomRows.SetEquals(new object[] { 1, 2, 4 }) && await new CountTableRowsBlock { Table = "quotes" }.EvaluateObjectAsync(dctx) is 3);
            var setRow = new SetTableRowAction { Table = "quotes" };
            foreach (var (column, value) in new[] { ("quote", "row {rowId} by {userLogin}"), ("year", "2026") })
            {
                var pair = setRow.Values.Items[^1]; // filling the empty row adds the next one
                pair.Key.SetText(column);
                pair.Value.SetText(value);
            }
            Check("data blocks: set table row sets each column", await Do(setRow) && reloaded.FindTable("quotes")!.FindRow(4)!.Get("quote") is "row 4 by budder" && reloaded.FindTable("quotes")!.FindRow(4)!.Get("year") is 2026);
            setRow.Values.Items[1].Value.SetText("soon");
            Check("data blocks: set table row stops at a value that doesn't fit", !await Do(setRow));
            var setRowCopy = (SetTableRowAction)BlockResolver.DeepCopy(setRow)!;
            Check("data blocks: set table row is saved and loaded", setRowCopy.Table == "quotes" && setRowCopy.Values.Items.Count == 3 && setRowCopy.Values.Items[1].Value.Text == "soon");
            Check("data blocks: get table row puts every column in parameters", await Do(new GetTableRowAction { Table = "quotes" }) && dctx.Parameters["quote"] == "row 4 by budder" && dctx.Parameters["year"] == "2026" && dctx.Parameters["id"] == "4");
            Check("data blocks: get table row with a prefix", await Do(With(new GetTableRowAction { Table = "quotes", Prefix = "q_" }, "1")) && dctx.Parameters["q_id"] == "1" && dctx.Parameters["q_quote"] == "hello there");
            Check("data blocks: get table row fails on a missing row", !await Do(With(new GetTableRowAction { Table = "quotes" }, "99")));
            Check("data blocks: delete table row", await Do(With(new DeleteTableRowAction { Table = "quotes" }, "{rowId}")) && await new CountTableRowsBlock { Table = "quotes" }.EvaluateObjectAsync(dctx) is 2);

            // cooldowns: a built-in table of when each label last went through
            var t0 = new DateTime(2026, 10, 31, 12, 0, 0);
            Check("cooldown: first use goes through, then waits (label case ignored), then goes again", reloaded.UseCooldown("hydrate", 30, t0, out _)
                && !reloaded.UseCooldown("HYDRATE", 30, t0.AddSeconds(10), out var cooldownLeft) && Math.Abs(cooldownLeft - 20) < 0.01
                && reloaded.UseCooldown("hydrate", 30, t0.AddSeconds(30), out _) && reloaded.UseCooldown("other", 30, t0.AddSeconds(31), out _)
                && reloaded.FindTable("cooldowns") is { IsBuiltIn: true } cooldowns && cooldowns.Rows.Count == 2);
            var cooldown = new CooldownBlock();
            ((ValueSlot)cooldown.Slots[0]).SetText("test-{userLogin}");
            ((ValueSlot)cooldown.Slots[1]).SetText("60");
            ((BlockSlot)cooldown.Slots[2]).TrySetBlock(new RecordBlock { Tag = "go" });
            async Task<string> RunCooldown() { RecordBlock.Log.Clear(); await cooldown.RunAsync(dctx); return string.Join("", RecordBlock.Log); }
            Check("cooldown block: runs, then does nothing while cooling down", await RunCooldown() == "go" && await RunCooldown() == ""
                && reloaded.FindTable("cooldowns")!.Rows.Any(r => r.Get("label") is "test-budder") && cooldown.Slots.Count == 3);
            Check("cooldown block: saved and loaded, under Control", BlockResolver.DeepCopy(cooldown) is CooldownBlock copiedCooldown && ((ValueSlot)copiedCooldown.Slots[0]).Text == "test-{userLogin}"
                && ((BlockSlot)copiedCooldown.Slots[2]).Block is RecordBlock && new CooldownBlock().Group == BlockGroup.Control);
            Check("data blocks: a missing table fails", !await Do(new AddTableRowAction { Table = "nope" }));
            Check("data blocks: under the Data heading, saved and loaded", new SetVariableAction().Group == BlockGroup.Data && new CountTableRowsBlock().Group == BlockGroup.Data
                && BlockResolver.DeepCopy(new GetVariableBlock { Variable = "deaths" }) is GetVariableBlock { Variable: "deaths", Kind: ValueKind.Int });

            // the Data tab's grid follows the model, and edits go through it
            var grid = new StreamAssist.Components.Data.UI.TableGridViewModel(reloaded, reloaded.FindTable("quotes")!);
            Check("data tab: the grid shows the table", grid.Rows.Count == 2 && grid.Columns.Select(c => c.Name).SequenceEqual(new[] { "quote", "year" }) && grid.Rows[0].Cells[0].Text == "hello there" && grid.IsCustom);
            reloaded.TrySetValue("quotes", 1, "quote", "changed by a block");
            reloaded.TryAddRow("quotes", out _);
            Check("data tab: changes from anywhere show up live", grid.Rows[0].Cells[0].Text == "changed by a block" && grid.Rows.Count == 3);
            grid.Rows[0].Cells[1].Text = "not a year"; grid.Rows[0].Cells[1].Commit(); // typed, then the box is left
            Check("data tab: a refused edit goes back", grid.Rows[0].Cells[1].Text == "2024" && reloaded.FindTable("quotes")!.FindRow(1)!.Get("year") is 2024);
            grid.Rows[0].Cells[1].Text = "1990"; grid.Rows[0].Cells[1].Commit(); // typed, then the box is left
            grid.Rows[2].DeletePressedCommand.Execute(null);
            Check("data tab: edits and deletes go to the model", reloaded.FindTable("quotes")!.FindRow(1)!.Get("year") is 1990 && grid.Rows.Count == 2 && reloaded.FindTable("quotes")!.Rows.Count == 2);
            grid.Columns[1].KindName = "Text";
            Check("data tab: changing a column's type rebuilds the grid", reloaded.FindTable("quotes")!.FindColumn("year")!.Kind == ValueKind.Text && grid.Columns[1].KindName == "Text");
            var varGrid = new StreamAssist.Components.Data.UI.TableGridViewModel(reloaded, reloaded.FindTable("variables")!);
            Check("data tab: built-in tables' columns can't be edited", !varGrid.IsCustom && varGrid.Columns.All(c => !c.IsEditable));
            grid.Dispose();
            varGrid.Dispose();

            // searching and sorting rows
            reloaded.TryCreateTable("scores");
            reloaded.TryAddColumn("scores", "name", ValueKind.Text);
            reloaded.TryAddColumn("scores", "points", ValueKind.Int);
            foreach (var (who, points) in new[] { ("bea", "5"), ("al", "20"), ("cy", "") })
            {
                reloaded.TryAddRow("scores", out var scoreRow);
                reloaded.TrySetValue("scores", scoreRow.Id, "name", who);
                reloaded.TrySetValue("scores", scoreRow.Id, "points", points);
            }
            var scores = new StreamAssist.Components.Data.UI.TableGridViewModel(reloaded, reloaded.FindTable("scores")!);
            string Order() => string.Join(",", scores.Rows.Select(r => r.Cells[0].Text));
            Check("data tab: rows start in id order, nothing sorted", Order() == "bea,al,cy" && scores.IdSortText == "↕" && scores.Columns.All(c => c.SortText == "↕"));
            scores.Columns[1].SortPressedCommand.Execute(null);
            Check("data tab: sort least to greatest, empty cells last", Order() == "bea,al,cy" && scores.Columns[1].SortText == "▲");
            scores.Columns[1].SortPressedCommand.Execute(null);
            Check("data tab: then greatest to least, empty cells still last", Order() == "al,bea,cy" && scores.Columns[1].SortText == "▼");
            scores.Columns[0].SortPressedCommand.Execute(null);
            Check("data tab: another column starts least to greatest (text ignores case)", Order() == "al,bea,cy" && scores.Columns[0].SortText == "▲" && scores.Columns[1].SortText == "↕");
            scores.Columns[0].SortPressedCommand.Execute(null);
            scores.Columns[0].SortPressedCommand.Execute(null);
            Check("data tab: a third press goes back to id order", Order() == "bea,al,cy" && scores.Columns[0].SortText == "↕");
            scores.SortByIdPressedCommand.Execute(null);
            Check("data tab: id can sort greatest first", Order() == "cy,al,bea" && scores.IdSortText == "▼");
            scores.SortByIdPressedCommand.Execute(null);
            scores.SearchText = "e";
            Check("data tab: search shows rows where any cell has the text", Order() == "bea");
            scores.SearchText = "20";
            Check("data tab: search finds numbers too", Order() == "al");
            scores.SearchText = "";
            Check("data tab: clearing the search shows every row", Order() == "bea,al,cy");

            // columns from the grid: typing a name in the empty box adds one, clearing an empty column's name removes it,
            // Delete columns mode shows a − on each that deletes it with its values
            scores.NewColumnName = "bonus"; scores.Commit(); // typed, then the box is left
            Check("data tab: a name in the new column box adds a text column", reloaded.FindTable("scores")!.FindColumn("bonus") is { Kind: ValueKind.Text }
                && scores.NewColumnName == "" && scores.Columns.Select(c => c.Name).SequenceEqual(new[] { "name", "points", "bonus" }));
            scores.Columns[2].NameInput = ""; scores.Columns[2].Commit(); // typed, then the box is left
            Check("data tab: clearing an empty column's name removes it", reloaded.FindTable("scores")!.FindColumn("bonus") == null && scores.Columns.Count == 2);
            scores.Columns[1].NameInput = "  "; scores.Columns[1].Commit(); // typed, then the box is left
            Check("data tab: clearing a column with values puts its name back", reloaded.FindTable("scores")!.FindColumn("points") != null && scores.Columns[1].NameInput == "points");
            Check("data tab: delete mode starts off", !scores.IsDeleteColumnsMode && scores.Columns.All(c => !c.IsDeleteMode));
            scores.DeleteColumnsPressedCommand.Execute(null);
            Check("data tab: Delete columns shows a − on every column", scores.IsDeleteColumnsMode && scores.Columns.All(c => c.IsDeleteMode));
            scores.Columns[1].DeletePressedCommand.Execute(null);
            Check("data tab: the − deletes the column, values and all, and the mode stays on", reloaded.FindTable("scores")!.FindColumn("points") == null
                && scores.Columns.Count == 1 && scores.Columns[0].IsDeleteMode);
            scores.DeleteColumnsPressedCommand.Execute(null);
            Check("data tab: pressing it again turns it off", !scores.IsDeleteColumnsMode && !scores.Columns[0].IsDeleteMode);

            // rows from the grid: a value typed in the empty row makes a row, clearing a row's every cell deletes it
            var rowsBefore = scores.Rows.Count;
            Check("data tab: the empty row has a cell per column, the first says what it's for", scores.NewRowCells.Count == 1 && scores.NewRowCells[0].Placeholder == "New row…");
            scores.NewRowCells[0].Text = "dee"; scores.NewRowCells[0].Commit(); // typed, then the box is left
            var deeRow = reloaded.FindTable("scores")!.Rows.Last();
            Check("data tab: typing in the empty row adds a row with the value", deeRow.Get("name") is "dee" && scores.Rows.Count == rowsBefore + 1
                && scores.NewRowCells[0].Text == "");
            scores.Rows.Single(r => r.Id == deeRow.Id).Cells[0].Text = ""; scores.Rows.Single(r => r.Id == deeRow.Id).Cells[0].Commit(); // typed, then the box is left
            Check("data tab: clearing a row's last value deletes it", reloaded.FindTable("scores")!.FindRow(deeRow.Id) == null && scores.Rows.Count == rowsBefore);
            reloaded.TryAddColumn("scores", "points", ValueKind.Int);
            var countBefore = reloaded.FindTable("scores")!.Rows.Count;
            scores.NewRowCells[1].Text = "lots"; scores.NewRowCells[1].Commit(); // typed, then the box is left
            Check("data tab: a value its column refuses leaves no row behind", reloaded.FindTable("scores")!.Rows.Count == countBefore && scores.NewRowCells[1].Text == "");
            scores.Rows[0].Cells[1].Text = "7"; scores.Rows[0].Cells[1].Commit(); // typed, then the box is left
            scores.Rows[0].Cells[1].Text = ""; scores.Rows[0].Cells[1].Commit(); // typed, then the box is left
            Check("data tab: clearing one cell of a row that has others keeps it", reloaded.FindTable("scores")!.FindRow(scores.Rows[0].Id) != null);
            scores.Dispose();

            // queries: terms that all have to match, by column or any cell
            reloaded.TryCreateTable("people");
            reloaded.TryAddColumn("people", "user", ValueKind.Text);
            reloaded.TryAddColumn("people", "points", ValueKind.Int);
            reloaded.TryAddColumn("people", "date", ValueKind.Text);
            foreach (var (user, points, date) in new[] { ("budder", "120", "2026-10-30"), ("rosebud", "5", "2026-09-01"), ("al", "", "2026-11-02"), ("two words", "50", "2026-10-05") })
            {
                reloaded.TryAddRow("people", out var person);
                reloaded.TrySetValue("people", person.Id, "user", user);
                reloaded.TrySetValue("people", person.Id, "points", points);
                reloaded.TrySetValue("people", person.Id, "date", date);
            }
            var people = reloaded.FindTable("people")!;
            string Q(string query) => RowQuery.TryParse(query, people, out _) is { } q
                ? string.Join(",", people.Rows.Where(r => q.Matches(people, r)).Select(r => r.Get("user"))) : "error";
            Check("query: a word or phrase matches any cell", Q("BUD") == "budder,rosebud" && Q("\"two words\"") == "two words" && Q("") == "budder,rosebud,al,two words");
            Check("query: contains, equals, not equals", Q("user:bud") == "budder,rosebud" && Q("user=BUDDER") == "budder" && Q("user!=budder") == "rosebud,al,two words"
                && Q("user=\"two words\"") == "two words");
            Check("query: numbers compare as numbers, empty cells never pass", Q("points>50") == "budder" && Q("points>=50") == "budder,two words" && Q("points<100") == "rosebud,two words"
                && Q("points=") == "al");
            Check("query: dates compare alphabetically, terms combine, id is a column", Q("date>2026-10-01") == "budder,al,two words" && Q("user:bud points>10") == "budder"
                && Q("id<=2") == "budder,rosebud");
            Check("query: an unknown column can't be run", RowQuery.TryParse("nope:x", people, out var queryError) == null && queryError == "people has no column 'nope'");
            var peopleGrid = new StreamAssist.Components.Data.UI.TableGridViewModel(reloaded, people);
            peopleGrid.QueryText = "points>10";
            Check("data tab: a query only applies when run", peopleGrid.Rows.Count == 4);
            peopleGrid.RunQueryPressedCommand.Execute(null);
            Check("data tab: running it filters the rows", peopleGrid.Rows.Select(r => r.Id).SequenceEqual(new[] { 1, 4 }));
            peopleGrid.SearchText = "words";
            Check("data tab: the search and the query both apply", peopleGrid.Rows.Select(r => r.Id).SequenceEqual(new[] { 4 }));
            peopleGrid.SearchText = "";
            peopleGrid.QueryText = "nope>1";
            peopleGrid.RunQueryPressedCommand.Execute(null);
            Check("data tab: a query that can't run leaves the rows as they were", peopleGrid.Rows.Select(r => r.Id).SequenceEqual(new[] { 1, 4 }));
            peopleGrid.QueryText = "";
            peopleGrid.RunQueryPressedCommand.Execute(null);
            Check("data tab: an empty query shows every row again", peopleGrid.Rows.Count == 4);
            peopleGrid.Dispose();

            StreamAssist.Shell.GlobalContext.DataModel = null;
            TriggerContext.VariableText = null;
            reloadedStore.Dispose();
            try { Directory.Delete(dataDir, true); } catch (IOException) { }
        }

        // ---- asset paths: blocks only reach files inside Assets/ ----
        {
            var assets = System.IO.Path.GetFullPath(StreamAssist.Core.Persistence.AppPaths.AssetsDir);
            bool In(string path, string expected) => StreamAssist.Core.Persistence.AppPaths.TryResolveAsset(path, out var full, out _)
                && full == System.IO.Path.Combine(assets, expected);
            bool Refused(string path) => !StreamAssist.Core.Persistence.AppPaths.TryResolveAsset(path, out _, out _);
            Check("assets: paths are inside Assets/", In("Sounds/boom.mp3", @"Sounds\boom.mp3") && In(@" assets/Sounds\boom.mp3 ", @"Sounds\boom.mp3")
                && In("Sounds/../Images/x.png", @"Images\x.png"));
            Check("assets: full paths and climbing out are refused", Refused(@"C:\Windows\win.ini") && Refused("/etc/passwd") && Refused("../Config/auth.json")
                && Refused("Sounds/../../Config/auth.json") && Refused("") && Refused("D:boom.mp3"));
            Check("assets: a picked file becomes its path inside Assets/, one outside isn't taken",
                StreamAssist.Core.Persistence.AppPaths.TryMakeAssetPath(System.IO.Path.Combine(assets, "Sounds", "boom.mp3"), out var picked) && picked == "Sounds/boom.mp3"
                && !StreamAssist.Core.Persistence.AppPaths.TryMakeAssetPath(System.IO.Path.Combine(assets, "..", "Config", "auth.json"), out _));
        }

        // ---- get time ----
        {
            var tctx = new TriggerContext(TriggerPhase.Once, default);
            var year = new GetTimeBlock { Setting = "Year" };
            var date = (string)await new GetTimeBlock().EvaluateObjectAsync(tctx);
            Check("get time: the date as text by default", new GetTimeBlock().Kind == ValueKind.Text && date == DateTime.Now.ToString("yyyy-MM-dd"));
            Check("get time: a number setting picks a whole number", year.Kind == ValueKind.Int && await year.EvaluateObjectAsync(tctx) is int y && y == DateTime.Now.Year);
            Check("get time: day of week and time as text", await new GetTimeBlock { Setting = "Day of Week" }.EvaluateObjectAsync(tctx) is string day && day == DateTime.Now.DayOfWeek.ToString()
                && await new GetTimeBlock { Setting = "Time" }.EvaluateObjectAsync(tctx) is string time && time.Length == 8 && time[2] == ':');
            Check("get time: saved and loaded keeps its setting and type", BlockResolver.DeepCopy(new GetTimeBlock { Setting = "Minute" }) is GetTimeBlock { Setting: "Minute", Kind: ValueKind.Int }
                && new GetTimeBlock().Group == BlockGroup.Text);
        }

        return failed;
    }
}
