using StreamAssist.Components.OBS.Model;
using StreamAssist.Components.Spotify.Actions;
using StreamAssist.Components.Spotify.Background;
using StreamAssist.Components.Spotify.Model;
using StreamAssist.Components.Spotify.Values;
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
using System.Text.Json;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Blocks.Containers;

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
        GetParameterBlock IntParam(string name) => new() { ParameterName = name, As = "Int" };
        GetParameterBlock DoubleParam(string name) => new() { ParameterName = name, As = "Double" };
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

            var missing = RunConsole(c => Slot<ValueSlot>(c, 0).BlockSlot.TrySetBlock(new GetParameterBlock { ParameterName = "user" }), noParams);
            Check("missing parameter: one error, run stopped", missing.Errors == 1 && missing.Ran.Count == 0);
            Check("the error names the block", LastError().Contains("error - Get Parameter: no parameter 'user'"));

            var notANumber = RunConsole(c => Slot<ValueSlot>(c, 0).BlockSlot.TrySetBlock(IntParam("bad")), ctx);
            Check("bad int parameter: one error", notANumber.Errors == 1 && LastError().Contains("bad is 'lots', not a whole number"));

            var typedBad = RunConsole(c => { var r = new RoundBlock(); Slot<ValueSlot>(r, 0).SetText("abc"); Slot<ValueSlot>(c, 0).BlockSlot.TrySetBlock(r); }, ctx);
            Check("typed value that isn't a number: one error naming block + slot", typedBad.Errors == 1 && LastError().Contains("error - Round: Number 'abc' is not a number"));

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
            var model = new ChatbotComponentModel();
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
            Check("roots: a fresh chatbot has an empty Match Text in the editable root", new ChatbotComponentModel().EditableRoot.Block is MatchTextChatBlock);
            Check("roots: editable root only takes a Match Text", !model.EditableRoot.Accepts(new HasPermissionChatBlock()) && !model.EditableRoot.Accepts(new ParameterChatBlock()));
            Check("roots: protected wins over editable", Send("!clash", Chat()) == "P");

            // matching
            Check("editable: plain row matches the whole word", Send("!rec", Chat()) == "R" && Send("!REC and more", Chat()) == "R");
            Check("editable: plain row doesn't match part of a word", Send("!recording", Chat()) == "");
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
            Check("delete: protected commands can't be touched", !model.TryDeleteCommand("#x") && !model.TryDeleteCommand("#") && xTarget.Bindable != null);
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
            var bare = new ChatbotComponentModel();
            bare.EditableRoot.RemoveBlock(bare.EditableRoot.Block!);
            Check("no block: refused", !bare.TryAddCommand("!a", "b") && LastError().Contains("no editable Match Text"));

            // save round trip
            var savedJson = JsonSerializer.Serialize(model);
            var loadedModel = JsonSerializer.Deserialize<ChatbotComponentModel>(savedJson)!;
            Check("round trip: json stable", JsonSerializer.Serialize(loadedModel) == savedJson);
            Check("round trip: commands still editable", loadedModel.TryAddCommand("!again", "x") && !loadedModel.TryAddCommand("!fresh", "x"));
            var emptyEditable = JsonSerializer.Deserialize<ChatbotComponentModel>(JsonSerializer.Serialize(bare))!;
            Check("round trip: a removed Match Text stays removed", emptyEditable.EditableRoot.Block == null);
            var oldFile = JsonSerializer.Deserialize<ChatbotComponentModel>("{\"rootBlock\":null}")!;
            Check("round trip: an older file gets an empty Match Text", oldFile.EditableRoot.Block is MatchTextChatBlock);
            model = loadedModel;
            Check("round trip: permission still gates", Send("#x", Chat()) == "" && Send("#x", Chat(mod: true)) == "X");
        }

        // ---- editable commands under a prefix row (Editable: Match Text [! -> Match Text [lurk -> send]]) ----
        {
            var m = new ChatbotComponentModel();
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
            var m = new ChatbotComponentModel();
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
            var m2 = new ChatbotComponentModel();
            var p = new ParameterChatBlock { ParameterName = "cmd" }; p.Target.TryBind(Seen("cmd"));
            ((MatchTextChatBlock)m2.Root.Block!).Rows.AddRow("!").Target.Child.TrySetBlock(p);
            string Cmd(string text) { ParamRecordBlock.Seen = null; m2.TryMatchMessage(new ChatMatchState(text, 0, new Dictionary<string, string>())); return ParamRecordBlock.Seen ?? "(none)"; }
            Check("param: after a symbol it reads right away", Cmd("!bob") == "bob" && Cmd("! bob") == "(none)");
            Check("param: shown glued to a symbol", m2.Describe(p.Target)?.Text == "!{cmd}");

            // editable commands only ever touch plain rows
            var e = new ChatbotComponentModel();
            var editable = (MatchTextChatBlock)e.EditableRoot.Block!;
            var catchAll = editable.Rows.AddRow("//"); catchAll.Target.TryBind(Rec("X"));
            var regexRow = editable.Rows.AddRow("/!r.*/"); regexRow.Target.TryBind(Rec("R"));
            Check("editable: a regex row isn't a command", !e.TryDeleteCommand("//", logErrors: false) && !e.TryDeleteCommand("/!r.*/", logErrors: false) && !e.TryDeleteCommand("!run", logErrors: false) && catchAll.Target.Bindable != null && regexRow.Target.Bindable != null);
            Check("editable: a command in slashes is refused", !e.TryAddCommand("/x/", "y", out var f, logErrors: false) && f == CommandFailure.Invalid);
            Check("editable: added commands are plain words", e.TryAddCommand("!hi", "yo") && editable.Rows.Rows.Any(r => r.Pattern == "!hi"));
        }

        // ---- Math, reward settings, Get / Set Reward Setting, New reward (main thread, errors are counted) ----
        {
            int Errors() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Count(l => l.Contains("] error - ")); }
            string LastError() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Last(l => l.Contains("] error - ")); }
            var redeem = new TriggerContext(TriggerPhase.Once, default, new Dictionary<string, string> { ["cost"] = "100", ["rewardId"] = "abc" });
            object Eval(ValueBlock block) => block.EvaluateObjectAsync(redeem).GetAwaiter().GetResult();
            // typing in the empty number at the end adds the next one, like the editor
            OperationBlock Op(string op, params string[] numbers)
            {
                var block = new OperationBlock { Operator = op };
                foreach (var number in numbers) block.Numbers.Items[^1].SetText(number);
                return block;
            }

            // operation: whole numbers stay whole like C# (a decimal block gives 101.0, an int slot gets 101)
            Check("operation: {cost} + 1", Eval(Op("+", "{cost}", "1")) is 101.0);
            Check("operation: the other operators", Eval(Op("−", "10", "4")) is 6.0 && Eval(Op("×", "6", "7")) is 42.0);
            Check("operation: whole numbers divide like C#", Eval(Op("÷", "7", "2")) is 3.0);
            Check("operation: a decimal anywhere gives a decimal answer", Eval(Op("÷", "7", "2.5")) is 2.8 && Eval(Op("+", "1.5", "1")) is 2.5);
            Check("operation: any number of numbers, top to bottom", Eval(Op("+", "1", "2", "3", "4")) is 10.0 && Eval(Op("−", "10", "3", "2")) is 5.0 && Eval(Op("÷", "100", "2", "5")) is 10.0);
            Check("operation: one empty slot always at the end", Op("+", "1", "2").Numbers.Items.Count == 3 && Op("+").Numbers.Items.Count == 1);
            var e0 = Errors();
            var divided = false;
            try { Eval(Op("÷", "1", "0")); } catch (BlockFailedException) { divided = true; }
            Check("operation: dividing by zero fails with one error", divided && Errors() == e0 + 1 && LastError().Contains("can't divide by zero"));
            Check("operation: is an any-type block", new OperationBlock().Category == BlockCategory.Any);
            var intSlot = new ValueSlot<int>("x");
            var placed = Op("+", "{cost}", "1");
            Check("operation: dropped in a whole number slot it gives a whole number", intSlot.BlockSlot.TrySetBlock(placed) && placed.Kind == ValueKind.Int && Eval(placed) is 101);
            var copied = (OperationBlock)BlockResolver.DeepCopy(placed)!;
            Check("operation: saved and loaded keeps its numbers and type", copied.Kind == ValueKind.Int && Eval(copied) is 101 && copied.Numbers.Items.Count == 3);

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
            ((ValueSlot)set.Slots[1]).BlockSlot.TrySetBlock(Op("+", "{cost}", "1"));
            var setList = new ListBlock();
            ((BlockSlotList)setList.Slots[0]).TryInsertBlock(set, 0);
            ((BlockSlotList)setList.Slots[0]).TryInsertBlock(new RecordBlock { Tag = "after" }, 1);
            RecordBlock.Log.Clear();
            var e2 = Errors();
            setList.SafeRunAsync(redeem).GetAwaiter().GetResult();
            Check("set reward setting: no chatbot fails with one error and stops", Errors() == e2 + 1 && RecordBlock.Log.Count == 0);
            Check("set reward setting: starts on the reward just redeemed", ((ValueSlot)new SetRewardSettingAction().Slots[0]).Text == "{reward}");

            // new reward: the model checks it, then asks the background
            var chatbot = new ChatbotComponentModel();
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
            var join = new JoinBlock();
            var parts = (ValueSlotList)join.Slots[0];
            parts.Items[^1].SetText("total: ");
            parts.Items[^1].BlockSlot.TrySetBlock(Expr("{cost}*({cost}+1)/2"));
            Check("expression: a whole answer reads cleanly as text", Eval(join) is "total: 325");
        }

        // ---- Set Parameter (main thread, errors are counted) ----
        {
            int Errors() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Count(l => l.Contains("] error - ")); }
            string LastError() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Last(l => l.Contains("] error - ")); }
            SetParameterAction SetP(string name, object value)
            {
                var set = new SetParameterAction { ParameterName = name };
                if (value is BlockModel block) set.Value.BlockSlot.TrySetBlock(block); else set.Value.SetText((string)value);
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
            Check("set parameter: no name fails and stops the run", RunList(given, "x", SetP(" ", "1")) == "(didn't run)" && Errors() == e0 + 1 && LastError().Contains("error - Set Parameter: needs a name"));
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
            var copied = (SetParameterAction)BlockResolver.DeepCopy(SetP("otherCost", Expr("{cost}+1")))!;
            Check("set parameter: saved and loaded keeps its name and value", copied.ParameterName == "otherCost" && RunList(given, "otherCost", copied) == "26");
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
            Check("obs blocks: switch scene", RunObs(Block(new SetObsSceneAction(), "Just Chatting")) && fake.CurrentScene == "Just Chatting");

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
            Check("voicemeeter: without voicemeeter the block fails once and stops", RecordBlock.Log.Count == 0 && Errors() == v1 + 1 && LastError().Contains("Mute/Unmute Voicemeeter: voicemeeter not set up"));

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
            var chatbot = new ChatbotManager(new ChatbotComponentModel());
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
            var m = new ChatbotComponentModel();
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
            var one = new XorCondition();
            var not = new NotCondition();
            Check("empty logic blocks are false", !await Eval(and) && !await Eval(or) && !await Eval(one) && !await Eval(not));
            Check("and", await Eval(WithList(new AndCondition(), true, true, true)) && !await Eval(WithList(new AndCondition(), true, false, true)));
            Check("or", await Eval(WithList(new OrCondition(), false, true)) && !await Eval(WithList(new OrCondition(), false, false)));
            Check("xor = odd number true", await Eval(WithList(new XorCondition(), false, true, false))
                && await Eval(WithList(new XorCondition(), true, true, true))
                && !await Eval(WithList(new XorCondition(), true, true))
                && !await Eval(WithList(new XorCondition(), false, false)));
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

            var list = new KeyboardListModel();
            var dev1 = new InputDeviceModel("dev1", "p", "m", 1, 2);
            var dev2 = new InputDeviceModel("dev2", "p", "m", 3, 4);
            list.AttachDevices(new List<InputDeviceModel> { dev1, dev2 });
            list.CreateSlot(out var slot);
            list.TryCreateConfig("c", null, out var cfg);
            slot.TrySetSelectedConfig(cfg); slot.TrySetSelectedDevice(dev1);
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
            Check("shift+T -> then", await Do(() => { Down("dev1", 0x10); Down("dev1", 0x54); }) == "S");
            Check("repeat key down ignored", await Do(() => Down("dev1", 0x54)) == "");
            await Do(() => { Up("dev1", 0x54); Up("dev1", 0x10); });
            Check("other keyboard doesn't trigger", await Do(() => Down("dev2", 0x54)) == "");
            Up("dev2", 0x54);
            Check("shift on other keyboard counts for Any keyboard", await Do(() => { Down("dev2", 0x10); Down("dev1", 0x54); }) == "S");
            Up("dev1", 0x54); Up("dev2", 0x10);

            Down("dev2", 0x41);
            var heldOk = input.IsKeyDown(0x41) && input.IsKeyDown("dev2", 0x41) && !input.IsKeyDown("dev1", 0x41);
            Up("dev2", 0x41);
            Check("IsKeyDown any/specific", heldOk && !input.IsKeyDown(0x41));

            // switching config/device takes effect immediately (no cached lookup)
            list.TryCreateConfig("c2", null, out var cfg2);
            var b2 = BindableModel.CreateDefault(); ((BlockSlot)b2.Root.Block!.Slots[0]).TrySetBlock(new RecordBlock { Tag = "2" }); cfg2.TryBindKey(0x54, b2);
            slot.TrySetSelectedConfig(cfg2);
            Check("config switch used right away", await Do(() => Down("dev1", 0x54)) == "2");
            Up("dev1", 0x54);
            slot.TrySetSelectedDevice(dev2);
            Check("device switch used right away", await Do(() => Down("dev2", 0x54)) == "2");
            Up("dev2", 0x54);

            // a bind that changes config while the trigger loop runs doesn't crash
            var swap = new BindableModel(); swap.Root.TrySetBlock(new SwapConfigBlock { Slot = slot, To = cfg });
            cfg2.TryBindKey(0x55, swap);
            Check("bind can switch config mid-trigger", await Do(() => Down("dev2", 0x55)) == "" && slot.SelectedConfig == cfg);
            Up("dev2", 0x55);
            km.Dispose();
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

            var model = new ChatbotComponentModel();
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
            var model = new ChatbotComponentModel();
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

            var model = new ChatbotComponentModel();
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
            Check("chat only as bot: off by default", !startup.ChatOnlyAsBot && !new ChatbotComponentModel().ChatOnlyAsBot);
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
            Check("int slot refuses a text block", !intSlot.BlockSlot.Accepts(new JoinBlock()));
            Check("double slot takes an int block", doubleSlot.BlockSlot.Accepts(IntParam("x")));
            Check("double slot takes Round (int)", doubleSlot.BlockSlot.Accepts(new RoundBlock()));
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

            var compare = new CompareCondition { Operator = "≥" };
            Slot<ValueSlot>(compare, 0).SetText("{viewers}");
            Slot<ValueSlot>(compare, 1).SetText("50");
            Check("Compare: viewers ≥ 50", await compare.EvaluateAsync(ctx));
            compare.Operator = "<";
            Check("Compare: viewers < 50 is false", !await compare.EvaluateAsync(ctx));

            var ignoreCase = new EqualsIgnoreCaseCondition();
            Slot<ValueSlot>(ignoreCase, 0).SetText("{user}"); Slot<ValueSlot>(ignoreCase, 1).SetText("VIEWER");
            Check("Equals Ignore Case", await ignoreCase.EvaluateAsync(ctx));

            Check("Convert to Bool", (bool)await MakeConvert("Bool", "{mod}").EvaluateObjectAsync(ctx));

            async Task<int> RoundWith(string mode, string number) { var r = new RoundBlock { Mode = mode }; Slot<ValueSlot>(r, 0).SetText(number); return await r.EvaluateAsync(ctx); }
            Check("Round modes", await RoundWith("Round", "2.5") == 3 && await RoundWith("Floor", "2.7") == 2 && await RoundWith("Ceiling", "2.1") == 3 && await RoundWith("Truncate", "-2.7") == -2);

            var join = new JoinBlock();
            var parts = Slot<ValueSlotList>(join, 0);
            Check("Join starts with one empty part", parts.Items.Count == 1 && parts.Items[0].Text == "");
            parts.Items[0].SetText("{user} raided with ");
            Check("typing in the last part adds an empty one below", parts.Items.Count == 2);
            parts.Items[1].BlockSlot.TrySetBlock(IntParam("viewers"));
            Check("placing a block in it adds another", parts.Items.Count == 3);
            parts.Items[2].SetText("!");
            Check("Join parts top to bottom, text + int + text", await join.EvaluateAsync(ctx) == "Viewer raided with 57!");
            var middle = parts.Items[1];
            middle.RemoveBlock(middle.BlockSlot.Block!);
            Check("an emptied part disappears", parts.Items.Count == 3 && await join.EvaluateAsync(ctx) == "Viewer raided with !");
            var joinJson = JsonSerializer.Serialize(BlockResolver.GetData(join));
            var joinLoaded = (JoinBlock)BlockResolver.Resolve(JsonSerializer.Deserialize<PendingBlockData>(joinJson)!)!;
            Check("Join round trip (the trailing empty part isn't saved)", JsonSerializer.Serialize(BlockResolver.GetData(joinLoaded)) == joinJson
                && Slot<ValueSlotList>(joinLoaded, 0).Items.Count == 3 && await joinLoaded.EvaluateAsync(ctx) == "Viewer raided with !");
            Check("Join parts know their block (for errors)", Slot<ValueSlotList>(joinLoaded, 0).Items.All(i => i.Owner == joinLoaded));

            Check("Convert to Int / Char / Double", (int)await MakeConvert("Int", "{viewers}").EvaluateObjectAsync(ctx) == 57
                && (char)await MakeConvert("Char", "!").EvaluateObjectAsync(ctx) == '!' && (double)await MakeConvert("Double", "{price}").EvaluateObjectAsync(ctx) == 2.5);

            // saving
            var saved = new ConsoleMessageAction();
            Slot<ValueSlot>(saved, 0).SetText("typed");
            Slot<ValueSlot>(saved, 0).BlockSlot.TrySetBlock(join);
            var savedJson = JsonSerializer.Serialize(BlockResolver.GetData(saved));
            var loadedBlock = BlockResolver.Resolve(JsonSerializer.Deserialize<PendingBlockData>(savedJson)!)!;
            Check("value slots round trip (typed text + block)", JsonSerializer.Serialize(BlockResolver.GetData(loadedBlock)) == savedJson && savedJson.Contains("\"text\":\"typed\""));
            Check("block list headings by type", new RoundBlock().Category == BlockCategory.Int && new JoinBlock().Category == BlockCategory.Text
                && new EqualsCondition().Category == BlockCategory.Condition && new ConvertBlock().Category == BlockCategory.Any && new GetParameterBlock().Category == BlockCategory.Any);
            Check("value blocks don't fit action slots", !new BlockSlotList("", typeof(ExecutableBlock)).Accepts(new JoinBlock()));
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
            var param = new GetParameterBlock { ParameterName = "viewers" }; // Text by default
            Check("int slot takes Get Parameter (it can be an int)", intSlot.BlockSlot.Accepts(param));
            intSlot.BlockSlot.TrySetBlock(param);
            Check("...and it becomes Int there", param.Kind == ValueKind.Int && param.As == "Int" && await intSlot.EvaluateAsync(ctx) == 57);
            Check("changing it to Text is refused (the slot needs an int)", !param.TrySetKind(ValueKind.Text) && param.Kind == ValueKind.Int);
            param.As = "Text";
            Check("...through the dropdown too", param.As == "Int");
            Check("changing to Double is refused too (double doesn't fit int)", !param.TrySetKind(ValueKind.Double));

            var textSlot = new ValueSlot<string>("Message");
            var intParam = IntParam("viewers");
            textSlot.BlockSlot.TrySetBlock(intParam);
            Check("an int Get Parameter already fits a text slot, so it stays Int", intParam.Kind == ValueKind.Int);
            Check("...and can change to anything, text takes all", intParam.TrySetKind(ValueKind.Bool) && intParam.TrySetKind(ValueKind.Int));
            Check("Get Parameter can't be a char", !new GetParameterBlock().CanBe(ValueKind.Char) && new ConvertBlock().CanBe(ValueKind.Char));

            var freeParam = new GetParameterBlock();
            Check("not placed: any allowed kind", freeParam.TrySetKind(ValueKind.Double) && freeParam.Kind == ValueKind.Double);

            // condition slots take any bool value block
            var ifCondition = new BlockSlot("", typeof(ConditionBlock));
            var convertToBool = MakeConvert("Text", "true");
            Check("condition slot takes Convert (it can be Bool)", ifCondition.Accepts(convertToBool));
            ifCondition.TrySetBlock(convertToBool);
            Check("...becomes Bool and evaluates", convertToBool.Kind == ValueKind.Bool && await ifCondition.EvaluateAsync(ctx));
            Check("condition slot refuses Join (text)", !ifCondition.Accepts(new JoinBlock()));
            var and = new AndCondition();
            ((BlockSlotList)and.Slots[0]).TryInsertBlock(MakeConvert("Bool", "true"), 0);
            ((BlockSlotList)and.Slots[0]).TryInsertBlock(new FixedCondition { Value = true }, 1);
            Check("And mixes conditions and bool value blocks", await and.EvaluateAsync(ctx));
            var branches = new BlockBranchList("");
            Check("If branches accept bool value blocks", branches.TryInsertBranch(MakeConvert("Text", "false"), 0) && branches.Branches[0].Condition.Block!.ParentSlot == branches.Branches[0].Condition);

            // the slot a block is in
            var slotJoin = new JoinBlock();
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
            var model = new ChatbotComponentModel();
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
            Check("Delete Message defaults to the triggering message", ((ValueSlot)new DeleteChatMessageAction().Slots[0]).Text == "{messageId}");
            ((ValueSlot)timeout.Slots[1]).SetText("{seconds}");
            var timeoutJson = JsonSerializer.Serialize(BlockResolver.GetData(timeout));
            var timeoutLoaded = BlockResolver.Resolve(JsonSerializer.Deserialize<PendingBlockData>(timeoutJson)!)!;
            Check("Time Out User round trip (a saved value isn't replaced by the default)",
                JsonSerializer.Serialize(BlockResolver.GetData(timeoutLoaded)) == timeoutJson && ((ValueSlot)timeoutLoaded.Slots[1]).Text == "{seconds}");
            Check("moderation blocks are actions, in the block list", new TimeOutUserAction().Category == BlockCategory.Action && new BanUserAction().Category == BlockCategory.Action
                && new DeleteChatMessageAction().Category == BlockCategory.Action && BlockResolver.DiscoverAll().Any(b => b is SendChatMessageAction));
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


        return failed;
    }
}
