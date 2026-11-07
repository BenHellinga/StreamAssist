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



// engine checks: the block system: bindables, If, loops, logic, bind labels, copy / paste
internal static partial class EngineChecks
{
    // helpers



    private static BindableModel Build()
    {
        var b = new BindableModel();
        var list = new ListBlock();
        b.TryAddBlock(list);
        var listSlot = (BlockSlotList)list.Slots[0];
        var p = new OnPressedBlock(); ((BlockSlot)p.Slots[0]).TrySetBlock(new RecordBlock { Tag = "A" });
        var r = new OnReleasedBlock(); ((BlockSlot)r.Slots[0]).TrySetBlock(new RecordBlock { Tag = "B" });
        listSlot.TryInsertBlock(p, 0);
        listSlot.TryInsertBlock(r, 1);
        listSlot.TryInsertBlock(new RecordBlock { Tag = "C" }, 2);
        return b;
    }



    private static async Task<string> Run(BindableModel b, Action<BindableModel> trigger)
    {
        RecordBlock.Log.Clear();
        trigger(b);
        await Task.Delay(50);
        return string.Join("", RecordBlock.Log);
    }



    private static IfBlock MakeIf(params (bool? cond, string? then)[] branches)
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



    private static async Task<string> RunIf(IfBlock ifb) { var bb2 = new BindableModel(); bb2.TryAddBlock(ifb); return await Run(bb2, x => x.TriggerOnce()); }



    // ---- bind labels ----
    private static async Task BindLabels()
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
        bind.Root.RemoveBlock(bind.Root.Blocks.FirstOrDefault()!);
        Check("label: removing the last block clears it", bind.Label == "" && !bind.HasBlock);
        bind.SetLabel("Nothing bound");
        Check("label: no blocks, no label", bind.Label == "");

        // the editor's label box: only a saved binding with blocks can be labelled
        var editor = new StreamAssist.Components.Blocks.UI.BindableEditorViewModel(new StreamAssist.Shell.WindowLayoutModel(), "nothing");
        BindableModel? saved = null;
        editor.ShowDraft("Key: X", draft => { saved = draft; return true; });
        Check("editor label: a draft can't be labelled yet", !editor.CanEditBindLabel);
        ((BlockSlot)editor.Bindable!.Root.Blocks.FirstOrDefault()!.Slots[0]).TrySetBlock(new RecordBlock { Tag = "x" });
        Check("editor label: once the draft is saved it can", saved != null && editor.CanEditBindLabel);
        editor.BindLabel = "My bind";
        Check("editor label: typing sets the binding's label", saved!.Label == "My bind");
        saved.Root.RemoveBlock(saved.Root.Blocks.FirstOrDefault()!);
        Check("editor label: removing the last block clears it and locks the box", saved.Label == "" && editor.BindLabel == "" && !editor.CanEditBindLabel);
        editor.Dispose();

        // the editor's Test: runs the binding with the Parameters list's values (samples first), plus extra rows
        var testEditor = new StreamAssist.Components.Blocks.UI.BindableEditorViewModel(new StreamAssist.Shell.WindowLayoutModel(), "nothing");
        var tested = new BindableModel();
        tested.TryAddBlock(new ParamRecordBlock { Key = "user" });
        testEditor.ShowBindable(tested, "x", [new ParameterInfo("user", ValueKind.Text, "who", "TestUser"), new ParameterInfo("bits", ValueKind.Int, "how many")]);
        Check("editor test: listed parameters start with their sample or a default", testEditor.TestParameters[0].Value == "TestUser" && testEditor.TestParameters[1].Value == "1"
            && testEditor.ExtraParameters.Count == 1 && testEditor.ExtraParameters[0].IsEmpty);
        ParamRecordBlock.Seen = null;
        testEditor.TestPressedCommand.Execute(null);
        await Task.Delay(50);
        Check("editor test: runs it with those values", ParamRecordBlock.Seen == "TestUser");
        testEditor.TestParameters[0].Value = "Bob";
        testEditor.ExtraParameters[0].Name = "extra";
        Check("editor test: typing in the extra row adds the next", testEditor.ExtraParameters.Count == 2);
        ((ParamRecordBlock)tested.Root.Blocks[0]).Key = "extra";
        testEditor.ExtraParameters[0].Value = "5";
        testEditor.TestPressedCommand.Execute(null);
        await Task.Delay(50);
        Check("editor test: extra rows are passed too", ParamRecordBlock.Seen == "5");
        testEditor.ExtraParameters[0].Name = "";
        testEditor.ExtraParameters[0].Value = "";
        Check("editor test: an emptied extra row goes", testEditor.ExtraParameters.Count == 1);
        testEditor.ShowBindable(tested, "y", [new ParameterInfo("user", ValueKind.Text, "who", "TestUser")]);
        Check("editor test: typed values are kept by name across bindings", testEditor.TestParameters[0].Value == "Bob");
        testEditor.Dispose();
        tested.Dispose();

        // the key editor: the default On Pressed only comes with selecting an unbound key
        var keyEditor = new StreamAssist.Components.Input.UI.KeybindViewModel(new StreamAssist.Shell.WindowLayoutModel());
        var keyConfig = new KeybindConfigModel("Test", null);
        var key7 = new StreamAssist.Components.Input.UI.KeyViewModel("7", 103, 0, 0, 1, 1, keyEditor);
        keyEditor.SetSelectedKey(key7, null, keyConfig);
        Check("key editor: selecting an unbound key starts with On Pressed", keyEditor.Bindable?.Root.Blocks.FirstOrDefault() is OnPressedBlock);
        ((BlockSlot)keyEditor.Bindable!.Root.Blocks.FirstOrDefault()!.Slots[0]).TrySetBlock(new RecordBlock { Tag = "x" });
        var boundKey = keyConfig.Keybinds[103];
        Check("key editor: its first change binds it", keyEditor.Bindable == boundKey);
        boundKey.Root.RemoveBlock(boundKey.Root.Blocks.FirstOrDefault()!);
        Check("key editor: removing every block unbinds it and nothing comes back", !keyConfig.Keybinds.ContainsKey(103) && keyEditor.Bindable != null && keyEditor.Bindable.Root.Blocks.FirstOrDefault() == null);
        keyEditor.Bindable!.TryAddBlock(new RecordBlock { Tag = "y" });
        Check("key editor: dropping a block in binds it again", keyConfig.Keybinds.ContainsKey(103));
        keyEditor.SetSelectedKey(new StreamAssist.Components.Input.UI.KeyViewModel("8", 104, 0, 0, 1, 1, keyEditor), null, keyConfig);
        Check("key editor: selecting another unbound key starts with On Pressed again", keyEditor.Bindable?.Root.Blocks.FirstOrDefault() is OnPressedBlock);
        keyEditor.Dispose();
    }



    // ---- a bindable runs its tree: List[ OnPressed(Rec A), OnReleased(Rec B), Rec C ] ----
    private static async Task Bindables()
    {
        var b = Build();
        Check("start runs pressed + plain", await Run(b, x => x.TriggerStart()) == "AC");
        Check("stop runs released + plain", await Run(b, x => x.TriggerStop()) == "BC");
        Check("once runs both", await Run(b, x => x.TriggerOnce()) == "ABC");

        // disable OnPressed: A skipped, but A itself not disabled
        var listSlot0 = (BlockSlotList)b.Root.Blocks.FirstOrDefault()!.Slots[0];
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
        var bb = new BindableModel(); bb.TryAddBlock(boom);
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
        Log(json);

        var d = BindableModel.CreateDefault();
        Check("default has On Pressed", d.Root.Blocks.FirstOrDefault() is OnPressedBlock);

        // ContainsSlot
        var outer = new ListBlock(); var inner = new OnPressedBlock();
        ((BlockSlotList)outer.Slots[0]).TryInsertBlock(inner, 0);
        Check("block contains its descendant slot", outer.ContainsSlot(inner.Slots[0]));
        Check("block doesn't contain unrelated slot", !inner.ContainsSlot(outer.Slots[0]));

        // cancellation on dispose: a slow block
        var slow = new BindableModel(); slow.TryAddBlock(new RecordBlock { Tag = "S", DelayMs = 500 });
        RecordBlock.Log.Clear(); slow.TriggerOnce(); slow.Dispose(); await Task.Delay(700);
        Check("dispose cancels running tree quietly", string.Join("", RecordBlock.Log) == "S");
    }



    // ---- If block ----
    private static async Task IfBranches()
    {
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
        var bif = new BindableModel(); bif.TryAddBlock(ifb5);
        var ifJson = JsonSerializer.Serialize(bif);
        var bifCopy = JsonSerializer.Deserialize<BindableModel>(ifJson)!;
        Check("if: round trip keeps behavior", await Run(bifCopy, x => x.TriggerOnce()) == "2" && JsonSerializer.Serialize(bifCopy) == ifJson);
        Log(ifJson);

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
    }



    // ---- empty bindables remove themselves ----
    private static async Task EmptyBindables()
    {
        var cfg = new StreamAssist.Components.Input.Model.KeybindConfigModel("t", null);
        var unbound = new List<ushort>(); cfg.KeyUnbound += (vk, _) => unbound.Add(vk);
        var kb = BindableModel.CreateDefault();
        cfg.TryBindKey(65, kb);
        kb.Root.RemoveBlock(kb.Root.Blocks.FirstOrDefault()!);
        Check("removing root unbinds the key", !cfg.Keybinds.ContainsKey(65) && unbound.SequenceEqual(new ushort[] { 65 }));

        var kb2 = BindableModel.CreateDefault(); cfg.TryBindKey(66, kb2);
        var kbInner = (BlockSlot)kb2.Root.Blocks.FirstOrDefault()!.Slots[0]; kbInner.TrySetBlock(new RecordBlock()); kbInner.RemoveBlock(kbInner.Block!);
        Check("removing a nested block keeps the binding", cfg.Keybinds.ContainsKey(66));
        kb2.Clear();
        Check("Clear() unbinds the key", !cfg.Keybinds.ContainsKey(66));

        // the root is a list: blocks run top to bottom, a binding stays while any are left
        var kb3 = new BindableModel();
        kb3.TryAddBlock(new RecordBlock { Tag = "1" });
        kb3.TryAddBlock(new RecordBlock { Tag = "2" });
        cfg.TryBindKey(67, kb3);
        RecordBlock.Log.Clear();
        kb3.TriggerOnce();
        await Task.Delay(50);
        Check("root list: every root block runs, top to bottom", RecordBlock.Log.SequenceEqual(["1", "2"]));
        kb3.Root.RemoveBlock(kb3.Root.Blocks[0]);
        Check("root list: removing one of two keeps the binding", cfg.Keybinds.ContainsKey(67) && kb3.Root.Blocks.Count == 1);
        var stopped = new BindableModel();
        stopped.TryAddBlock(new RecordBlock { Tag = "a", Throw = true });
        stopped.TryAddBlock(new RecordBlock { Tag = "b" });
        RecordBlock.Log.Clear();
        stopped.TriggerOnce();
        await Task.Delay(50);
        Check("root list: a failing block ends the run, like in a List", !RecordBlock.Log.Contains("b"));
        Check("root list: saved as an array", JsonSerializer.Serialize(kb3).Contains("\"root\":[{"));
        kb3.Clear();
        stopped.Dispose();

        var oldJson = "{\"name\":\"x\",\"bindables\":{\"55\":{\"isDisabled\":false,\"root\":[]},\"70\":{\"isDisabled\":false,\"root\":[{\"type\":\"OnPressedBlock\",\"parameters\":{\"child\":null,\"isDisabled\":false}}]}}}";
        var loaded = JsonSerializer.Deserialize<StreamAssist.Components.Input.Model.KeybindConfigModel>(oldJson)!;
        Check("empty bindables dropped on load", !loaded.Keybinds.ContainsKey(55) && loaded.Keybinds.ContainsKey(70));
        loaded.Keybinds[70].Clear();
        Check("loaded bindable can still unbind itself", loaded.Keybinds.Count == 0);
        Check("saved json has no empty bindings", !JsonSerializer.Serialize(loaded).Contains("\"root\":[]"));

        var copyCfg = new StreamAssist.Components.Input.Model.KeybindConfigModel("c", new() { [80] = BindableModel.CreateDefault(), [81] = new BindableModel() });
        Check("copied config drops empties", copyCfg.Keybinds.ContainsKey(80) && !copyCfg.Keybinds.ContainsKey(81));
        copyCfg.Keybinds[80].Clear();
        Check("copied config bindable unbinds itself", copyCfg.Keybinds.Count == 0);
    }



    // ---- logic conditions ----
    private static async Task LogicConditions()
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
        var hb = new BindableModel(); hb.TryAddBlock(holder);
        var hj = JsonSerializer.Serialize(hb);
        Check("nested logic round trip", JsonSerializer.Serialize(JsonSerializer.Deserialize<BindableModel>(hj)!) == hj);
    }



    // ---- loops, wait, stop, random, text (runs go on a worker thread: on the UI thread a loop pauses



    // through the dispatcher, which nothing runs here. Failures are checked by the run stopping, not the console) ----
    private static async Task LoopsWaitStop()
    {
        T Slotted<T>(T block, params string[] slotTexts) where T : BlockModel
        {
            for (int i = 0; i < slotTexts.Length; i++) if (slotTexts[i] != null) ((ValueSlot)block.Slots.Where(s => s is not MenuSlot).ElementAt(i)).SetText(slotTexts[i]);
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
        Check("equals: no A / B labels, == shown between the values", ((ValueSlot)new EqualsCondition().Slots[2]).Connector == "==" && ((ValueSlot)new EqualsCondition().Slots[1]).Label == "");

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
            && (bool)await Value(Slotted(new TextContainsCondition().Pick("Check", "Starts With"), "!lurk now", "!LURK"))
            && !(bool)await Value(Slotted(new TextContainsCondition().Pick("Check", "Ends With"), "!lurk now", "lurk")));
        Check("text contains: match case", !(bool)await Value(Slotted(new TextContainsCondition().Pick("Case", "Match Case"), "Hello", "hello")));
        Check("change text: upper / lower / trim / capitalize", (string)await Value(Slotted(new ChangeTextBlock(), "Hi There")) == "HI THERE"
            && (string)await Value(Slotted(new ChangeTextBlock().Pick("Change", "Lower Case"), "Hi There")) == "hi there"
            && (string)await Value(Slotted(new ChangeTextBlock().Pick("Change", "Trim"), "  hi  ")) == "hi"
            && (string)await Value(Slotted(new ChangeTextBlock().Pick("Change", "Capitalize"), "budder")) == "Budder");
        Check("text length: characters and words", (int)await Value(Slotted(new TextLengthBlock(), "hello")) == 5
            && (int)await Value(Slotted(new TextLengthBlock().Pick("Count", "Words"), "  hi   there you ")) == 3);
        Check("replace text: every one, case ignored unless Match Case", (string)await Value(Slotted(new ReplaceTextBlock(), "Hello hello", "HELLO", "bye")) == "bye bye"
            && (string)await Value(Slotted(new ReplaceTextBlock().Pick("Case", "Match Case"), "Hello hello", "hello", "")) == "Hello ");
        Check("replace text: an empty Find fails", await Fails(Slotted(new ReplaceTextBlock(), "x", "", "y")));
        Check("get part: words, from the end, past the end", (string)await Value(Slotted(new GetPartBlock(), "!so  budder", "", "2")) == "budder"
            && (string)await Value(Slotted(new GetPartBlock(), "a b c", "", "-1")) == "c" && (string)await Value(Slotted(new GetPartBlock(), "a b", "", "5")) == "");
        Check("get part: a list separator", (string)await Value(Slotted(new GetPartBlock(), "Cam, Title, Mic", ", ", "2")) == "Title");
        Check("get part: number 0 fails", await Fails(Slotted(new GetPartBlock(), "a", "", "0")));

        // saving, and the block list
        var savedLoop = (RepeatBlock)BlockResolver.DeepCopy(Holding(Slotted(new RepeatBlock { NumberName = "n" }, "{times}"), 1, Say("{n}")))!;
        Check("logic blocks: saved and loaded keep their settings and children", savedLoop.NumberName == "n" && ((ValueSlot)savedLoop.Slots[0]).Text == "{times}"
            && ((BlockSlot)savedLoop.Slots[1]).Block is FormatRecordBlock { Template: "{n}" }
            && BlockResolver.DeepCopy(new TextContainsCondition().Pick("Check", "Ends With").Pick("Case", "Match Case")) is TextContainsCondition copiedContains && copiedContains.Picked("Check") == "Ends With" && copiedContains.Picked("Case") == "Match Case");
        var listed = BlockResolver.DiscoverAll().ToDictionary(b => b.GetType(), b => b.Group);
        Check("logic blocks: in the block list under their headings", listed[typeof(RepeatBlock)] == BlockGroup.Control && listed[typeof(WhileBlock)] == BlockGroup.Control
            && listed[typeof(ForEachBlock)] == BlockGroup.Control && listed[typeof(WaitAction)] == BlockGroup.Control && listed[typeof(StopAction)] == BlockGroup.Control
            && listed[typeof(ChanceCondition)] == BlockGroup.Logic && listed[typeof(TextContainsCondition)] == BlockGroup.Text
            && listed[typeof(RandomNumberBlock)] == BlockGroup.Math && listed[typeof(PickRandomBlock)] == BlockGroup.Text
            && listed[typeof(TextLengthBlock)] == BlockGroup.Text && listed[typeof(GetPartBlock)] == BlockGroup.Text);
    }



    // ---- get time ----
    private static async Task GetTime()
    {
        var tctx = new TriggerContext(TriggerPhase.Once, default);
        var year = new GetTimeBlock().Pick("Setting", "Year");
        var date = (string)await new GetTimeBlock().EvaluateObjectAsync(tctx);
        Check("get time: the date as text by default", new GetTimeBlock().Kind == ValueKind.Text && date == DateTime.Now.ToString("yyyy-MM-dd"));
        Check("get time: a number setting picks a whole number", year.Kind == ValueKind.Int && await year.EvaluateObjectAsync(tctx) is int y && y == DateTime.Now.Year);
        Check("get time: day of week and time as text", await new GetTimeBlock().Pick("Setting", "Day of Week").EvaluateObjectAsync(tctx) is string day && day == DateTime.Now.DayOfWeek.ToString()
            && await new GetTimeBlock().Pick("Setting", "Time").EvaluateObjectAsync(tctx) is string time && time.Length == 8 && time[2] == ':');
        Check("get time: saved and loaded keeps its setting and type", BlockResolver.DeepCopy(new GetTimeBlock().Pick("Setting", "Minute")) is GetTimeBlock { Kind: ValueKind.Int } copiedTime && copiedTime.Picked("Setting") == "Minute"
            && new GetTimeBlock().Group == BlockGroup.Text);
    }



    // ---- copy / paste (BlockClipboard, no system clipboard here: it's kept in the app) ----
    private static async Task CopyPaste()
    {
        var list = new StreamAssist.Components.Blocks.Containers.ListBlock();
        ((BlockSlotList)list.Slots[0]).TryInsertBlock(new WaitAction(), 0);
        StreamAssist.Components.Blocks.UI.BlockClipboard.CopyBlock(list);

        var script = new BindableModel();
        var root = new StreamAssist.Components.Blocks.UI.BlockSlotListViewModel(script.Root);
        var wait = new WaitAction();
        var seconds = new StreamAssist.Components.Blocks.UI.ValueSlotViewModel((ValueSlot)wait.Slots[0]);
        Check("paste: a block tree pastes where it fits, as its own copy",
            StreamAssist.Components.Blocks.UI.BlockClipboard.TryPasteOn(root, root) && script.Root.Blocks is [StreamAssist.Components.Blocks.Containers.ListBlock pasted]
            && pasted != list && ((BlockSlotList)pasted.Slots[0]).Blocks is [WaitAction]);
        Check("paste: an action doesn't fit a value slot", !StreamAssist.Components.Blocks.UI.BlockClipboard.CanPasteOn(seconds));

        ((ValueSlot)wait.Slots[0]).SetText("5");
        StreamAssist.Components.Blocks.UI.BlockClipboard.CopyBlock(new TextLengthBlock());
        Check("paste: a value block pastes over a value slot's typed text", StreamAssist.Components.Blocks.UI.BlockClipboard.TryPasteOn(seconds, seconds)
            && ((ValueSlot)wait.Slots[0]).BlockSlot.Block is TextLengthBlock);

        StreamAssist.Components.Blocks.UI.BlockClipboard.CopyBindable(script);
        var emptyRoot = new StreamAssist.Components.Blocks.UI.BlockSlotListViewModel(new BindableModel().Root);
        Check("paste: a whole binding doesn't paste into block slots", !StreamAssist.Components.Blocks.UI.BlockClipboard.CanPasteOn(emptyRoot));

        StreamAssist.Components.Blocks.UI.BlockClipboard.ReadText = () => Task.FromResult<string?>("just some text");
        await StreamAssist.Components.Blocks.UI.BlockClipboard.RefreshAsync();
        StreamAssist.Components.Blocks.UI.BlockClipboard.ReadText = null;
        Check("paste: text that isn't ours pastes nowhere", !StreamAssist.Components.Blocks.UI.BlockClipboard.CanPasteOn(emptyRoot));
    }



    // ---- Change Config: switches a tab's config by name, an unknown name stops the run ----
    private static async Task ChangeConfig()
    {
        var oldChat = StreamAssist.Shell.GlobalContext.ChatbotComponentModel;
        var chat = Chatbot();
        chat.ChatConfigs.TryCreate("Fools");
        chat.ChatConfigs.TrySelect("Default");
        StreamAssist.Shell.GlobalContext.ChatbotComponentModel = chat;

        async Task<bool> Change(string tab, string name)
        {
            RecordBlock.Log.Clear();
            var list = new ListBlock();
            var change = new ChangeConfigAction().Pick("Tab", tab);
            ((ValueSlot)change.Slots[1]).SetText(name);
            ((BlockSlotList)list.Slots[0]).TryInsertBlock(change, 0);
            ((BlockSlotList)list.Slots[0]).TryInsertBlock(new RecordBlock { Tag = "after" }, 1);
            await list.SafeRunAsync(new TriggerContext(TriggerPhase.Once, default));
            return RecordBlock.Log.Contains("after");
        }

        Check("change config: switches the tab (case ignored)", await Change("Chat", "fools") && chat.ChatConfigs.SelectedName == "Fools");
        Check("change config: the one in use is fine", await Change("Chat", "Fools") && chat.ChatConfigs.SelectedName == "Fools");
        Check("change config: only that tab changes", chat.EventsConfigs.SelectedName == "Default");
        Check("change config: an unknown name stops the run", !await Change("Chat", "Nope") && chat.ChatConfigs.SelectedName == "Fools");

        StreamAssist.Shell.GlobalContext.ChatbotComponentModel = oldChat;
        chat.Dispose();
    }



    // ---- dropdowns are slots: an option picked, or a block giving it (Random Option picks one) ----
    private static void MenuSlots()
    {
        var ctx = new TriggerContext(TriggerPhase.Once, default, new Dictionary<string, string> { ["mode"] = "toggle", ["bad"] = "sideways" });
        string Eval(MenuSlot slot) => slot.EvaluateAsync(ctx).GetAwaiter().GetResult();
        MenuSlot Menu() => new("Do", () => ["Default", "On", "Off", "Toggle"], "On");

        Check("dropdown: the picked option", Eval(Menu()) == "On");

        var fromBlock = Menu();
        var param = new ConvertBlock { To = "Text" };
        ((ValueSlot)param.Slots[0]).SetText("{mode}");
        fromBlock.BlockSlot.TrySetBlock(param);
        Check("dropdown: a block gives the option, case ignored, spelled as in the list", Eval(fromBlock) == "Toggle");

        var badBlock = new ConvertBlock { To = "Text" };
        ((ValueSlot)badBlock.Slots[0]).SetText("{bad}");
        var bad = Menu();
        bad.BlockSlot.TrySetBlock(badBlock);
        var refused = false;
        try { Eval(bad); } catch (BlockFailedException) { refused = true; }
        Check("dropdown: a fixed list refuses anything else", refused);

        var changing = new MenuSlot("Device", () => ["Default", "Speakers"], "Headphones", isFixed: false);
        Check("dropdown: a changing list passes an unknown one on (the block decides)", Eval(changing) == "Headphones");

        var random = Menu();
        Check("random option: fits a dropdown, not a plain value slot", random.BlockSlot.TrySetBlock(new RandomOptionBlock())
            && !new ValueSlot<string>("Text").BlockSlot.Accepts(new RandomOptionBlock()));
        var picked = Enumerable.Range(0, 60).Select(_ => Eval(random)).ToHashSet();
        Check("random option: picks among the options, never Default", picked.SetEquals(["On", "Off", "Toggle"]));

        var indicator = new StreamAssist.Components.Dashboard.Actions.SetIndicatorAction();
        ((ValueSlot)indicator.Slots[0]).BlockSlot.TrySetBlock(new RandomOptionBlock());
        var copy = (StreamAssist.Components.Dashboard.Actions.SetIndicatorAction)BlockResolver.DeepCopy(indicator)!;
        Check("dropdown: a block in it is saved and loaded", ((ValueSlot)copy.Slots[0]).BlockSlot.Block is RandomOptionBlock);
        indicator.Dispose();
        copy.Dispose();
    }



    // ---- the lists' type filter: the kinds of slot a block fits, clicking a slot picks its kind ----
    private static void ListTypeFilter()
    {
        Check("type filter: a slot's kind", SlotTags.TagOf(new ValueSlot<int>("n")) == "Int" && SlotTags.TagOf(new ValueSlot<int>("n").BlockSlot) == "Int"
            && SlotTags.TagOf(new MenuSlot("m", () => ["a"], "a")) == "Dropdown" && SlotTags.TagOf(new ValueSlot<bool>("b")) == "Condition"
            && SlotTags.TagOf(new BlockSlotList("Do", typeof(ExecutableBlock))) == "Action" && SlotTags.TagOf(new AnyValueSlot("x")) == "Any Value");
        Check("type filter: a block fits the kinds of slot that would take it", SlotTags.Fits(new RandomNumberBlock(), "Int") && SlotTags.Fits(new RandomNumberBlock(), "Text")
            && !SlotTags.Fits(new RandomNumberBlock(), "Action") && SlotTags.Fits(new RandomOptionBlock(), "Dropdown") && !SlotTags.Fits(new RandomOptionBlock(), "Text")
            && SlotTags.Fits(new WaitAction(), "Action") && SlotTags.Fits(new WaitAction(), SlotTags.All));

        var palette = new BlockPaletteViewModel(null, BlockGroup.Control, BlockGroup.Math);
        var maths = new BlockPaletteViewModel(null, BlockGroup.Math);
        Check("type filter: only the kinds the list has", palette.Tags[0] == SlotTags.All && palette.Tags.Contains("Action") && palette.Tags.Contains("Int")
            && !palette.Tags.Contains("Chat Block") && !maths.Tags.Contains("Action"));
        palette.SelectedTag = "Action";
        Check("type filter: picking a kind shows only what fits it", palette.Groups.SelectMany(g => g.Cards).All(c => SlotTags.Fits(c.Block, "Action"))
            && palette.Groups.SelectMany(g => g.Cards).Any() && !palette.Groups.Any(g => g.Group == BlockGroup.Math));
        SlotTags.Request(new ValueSlot<double>("d"));
        var picked = maths.SelectedTag;
        SlotTags.Request(new BlockSlotList("Do", typeof(ExecutableBlock)));
        Check("type filter: clicking a slot picks its kind, a kind the list doesn't have is ignored", picked == "Double" && maths.SelectedTag == "Double"
            && palette.SelectedTag == "Action");
        palette.Dispose();
        maths.Dispose();
    }
}
