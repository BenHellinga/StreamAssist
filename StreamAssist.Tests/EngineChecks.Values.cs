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



// engine checks: value slots, conversions, Expression, Set / Parse Parameter and the console errors bad values give
internal static partial class EngineChecks
{
    // ---- value errors (first, while the main thread still owns the dispatcher that AppLog posts to) ----
    private static void ValueErrors()
    {
        var ctx = new TriggerContext(TriggerPhase.Once, default, new Dictionary<string, string> { ["user"] = "Viewer", ["bad"] = "lots" });
        var noParams = new TriggerContext(TriggerPhase.Start, default);
        T Slot<T>(BlockModel b, int i) where T : BlockSlotBase => (T)b.Slots.Where(s => s is not MenuSlot).ElementAt(i);
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



    // ---- Expression: typed formulas (main thread, errors are counted) ----
    private static void Expression()
    {
        int Errors() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Count(l => l.Contains("] error - ")); }
        string LastError() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Last(l => l.Contains("] error - ")); }
        object? F(string text) => StreamAssist.Core.Utilities.Formula.TryEvaluate(text, new Dictionary<string, object>(), null, out var r, out _) ? r : null;
        string Err(string text) { StreamAssist.Core.Utilities.Formula.TryEvaluate(text, new Dictionary<string, object>(), null, out _, out var e); return e; }

        Check("formula: precedence and parentheses", F("2+3*4") is 14 && F("(2+3)*4") is 20 && F("10-3-2") is 5);
        Check("formula: triangle numbers", F("25*(25+1)/2") is 325 && F("(25-1)*25/2") is 300);
        Check("formula: whole division drops decimals like C#", F("7/2") is 3 && F("-7/2") is -3 && F("7%3") is 1);
        Check("formula: a decimal makes that part decimal", F("7/2.0") is 3.5 && F("7/2*2.0") is 6.0 && F("1.5+1") is 2.5);
        Check("formula: signs and spaces", F(" -3 + -(-2) ") is -1 && F("3- -5") is 8);
        Check("formula: mistakes are explained", Err("1/0").Contains("divide by zero") && Err("(1+2") != "" && Err("2+") != "" && Err("2 x 3") != "" && !Err("2 x 3").Contains("at index"));

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
        Check("expression: a bad formula fails with one error naming it", exprFailed && Errors() == e0 + 1 && LastError().Contains("error - Expression: '25/0' attempted to divide by zero"));
        var builder = new StringBuilderBlock();
        builder.Text.SetText("total: {t}");
        var tRow = builder.Values.Items[^1];
        tRow.Key.SetText("t");
        tRow.Value.BlockSlot.TrySetBlock(Expr("{cost}*({cost}+1)/2"));
        Check("expression: a whole answer reads cleanly as text", Eval(builder) is "total: 325");

        // true/false: comparisons and logic, like C#
        Check("formula: comparisons", F("3 == 3.0") is true && F("3 != 3") is false && F("2 < 3") is true && F("3 <= 2") is false && F("4 >= 4") is true);
        Check("formula: && || ! and true / false, C#'s precedence", F("1 < 2 && 2 < 3") is true && F("false || 1 == 1") is true && F("!(1 == 1)") is false
            && F("true && !false") is true && F("1 + 1 == 2 || false && false") is true);
        Check("formula: maths and true/false don't mix", Err("true + 1") != "" && Err("5 && 1") != "" && Err("5 == true") != "" && Err("1 = 1") != "");
        Check("formula: text, its methods and ? :", F("\"Bob\" == \"Bob\"") is true && F("\"Bob\" == \"bob\"") is false && F("\"hello\".Contains(\"ell\")") is true
            && F("\"Bob\".ToLower()") is "bob" && F("\"abc\".Length") is 3 && F("2 > 1 ? \"yes\" : \"no\"") is "yes" && F("Math.Sqrt(16)") is 4.0);
        Check("formula: no files, reflection or assigning", Err("\"a\".GetType().Assembly") != "" && Err("System.IO.File.Exists(\"x\")") != "" && Err("x = 5") != "");
        Check("formula: a value is read as a number, true/false or else text", StreamAssist.Core.Utilities.Formula.ReadValue("5") is 5 && StreamAssist.Core.Utilities.Formula.ReadValue("2.5") is 2.5 && StreamAssist.Core.Utilities.Formula.ReadValue("True") is true
            && StreamAssist.Core.Utilities.Formula.ReadValue("Bob") is "Bob" && StreamAssist.Core.Utilities.Formula.ReadValue("NaN") is "NaN" && StreamAssist.Core.Utilities.Formula.ReadValue("") is "");
        var ifCondition = new BlockBranchList("");
        ifCondition.TryInsertBranch(Expr("{cost} == 25 && !({cost} > 30)"), 0);
        var asCondition = (ExpressionBlock)ifCondition.Branches[0].Condition.Block!;
        Check("expression: in an If it gives true/false", asCondition.Kind == ValueKind.Bool && Eval(asCondition) is true);
        var e1 = Errors();
        var mixFailed = false;
        try { Eval(Expr("{cost} > 1")); } catch (BlockFailedException) { mixFailed = true; }
        Check("expression: true/false in a number slot fails naming it", mixFailed && Errors() == e1 + 1 && LastError().Contains("gives true/false, not a number"));

        // text: each {name} goes in as a value, so quotes or brackets in it can't change the formula
        var chat = new TriggerContext(TriggerPhase.Once, default, new Dictionary<string, string> { ["user"] = "Bob", ["message"] = "hi \"there\") || true", ["id"] = "1234" });
        object EvalIn(ValueBlock block, TriggerContext c) => block.EvaluateObjectAsync(c).GetAwaiter().GetResult();
        ExpressionBlock Check2(string text) { var b = new BlockBranchList(""); b.TryInsertBranch(Expr(text), 0); return (ExpressionBlock)b.Branches[0].Condition.Block!; }
        Check("expression: text comparisons", EvalIn(Check2("{user} == \"Bob\""), chat) is true && EvalIn(Check2("{user} == \"bob\""), chat) is false
            && EvalIn(Check2("{user}.ToLower() == \"bob\" && {message}.Contains(\"there\")"), chat) is true);
        Check("expression: a value with quotes stays one value", EvalIn(Check2("{message} == \"x\""), chat) is false);
        Check("expression: a number-looking value is a number, ToString() reads it as text", EvalIn(Check2("{id} == 1234"), chat) is true && EvalIn(Check2("{id}.ToString() == \"1234\""), chat) is true);
        var textSlot = new ValueSlot<string>("x");
        var greeting = Expr("\"Hi {user}, \" + ({cost} > 1 ? \"big\" : \"small\")");
        var both = new TriggerContext(TriggerPhase.Once, default, new Dictionary<string, string> { ["user"] = "Bob \"B\"", ["cost"] = "25" });
        Check("expression: in a text slot it gives text, {name} inside quotes is part of that text", textSlot.BlockSlot.TrySetBlock(greeting) && greeting.Kind == ValueKind.Text
            && EvalIn(greeting, both) is "Hi Bob \"B\", big");
        var e2 = Errors();
        var wordFailed = false;
        try { EvalIn(Check2("{user}==Bob"), chat); } catch (BlockFailedException) { wordFailed = true; }
        Check("expression: unquoted text fails showing the values", wordFailed && Errors() == e2 + 1 && LastError().Contains("Expression: 'Bob==Bob' unknown identifier 'Bob'"));
    }



    // ---- Set Parameter (main thread, errors are counted) ----
    private static void SetParameter()
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



    // ---- value blocks + value slots ----
    private static async Task ValueBlocksAndSlots()
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

        T Slot<T>(BlockModel b, int i) where T : BlockSlotBase => (T)b.Slots.Where(s => s is not MenuSlot).ElementAt(i);

        var equals = new EqualsCondition();
        Slot<ValueSlot>(equals, 0).BlockSlot.TrySetBlock(IntParam("viewers"));
        Slot<ValueSlot>(equals, 1).SetText("57");
        Check("Equals block: viewers == 57", await equals.EvaluateAsync(ctx));

        var ignoreCase = new EqualsCondition().Pick("Case", "Ignore Case");
        Slot<ValueSlot>(ignoreCase, 0).SetText("{user}"); Slot<ValueSlot>(ignoreCase, 1).SetText("VIEWER");
        Check("Equals: Ignore Case", await ignoreCase.EvaluateAsync(ctx));
        ignoreCase.Pick("Case", "Match Case");
        Check("Equals: Match Case (the default) doesn't", !await ignoreCase.EvaluateAsync(ctx) && new EqualsCondition().Picked("Case") == "Match Case"
            && BlockResolver.DeepCopy(new EqualsCondition().Pick("Case", "Ignore Case")) is EqualsCondition copiedEquals && copiedEquals.Picked("Case") == "Ignore Case");

        Check("Convert to Bool", (bool)await MakeConvert("Bool", "{mod}").EvaluateObjectAsync(ctx));

        // Expression's functions (Round is gone)
        double? Fn(string text) => StreamAssist.Core.Utilities.Formula.TryEvaluate(text, new Dictionary<string, object>(), null, out var n, out _) ? Convert.ToDouble(n) : null;
        Check("expression functions", Fn("round(2.5)") == 3 && Fn("round(-2.5)") == -3 && Fn("floor(2.7)") == 2 && Fn("ceil(2.1)") == 3 && Fn("truncate(-2.7)") == -2
            && Fn("abs(-4)") == 4 && Fn("log10(1000)") == 3 && Fn("pow(2, 10)") == 1024 && Fn("max(1, 7, 3)") == 7 && Fn("min(2.5, 1)") == 1 && Fn("round(10 / 4.0) * 2") == 6);
        Check("expression functions: rounding gives a whole number, mistakes fail", StreamAssist.Core.Utilities.Formula.TryEvaluate("round(7 / 2.0)", new Dictionary<string, object>(), null, out var rounded, out _) && rounded is 4
            && StreamAssist.Core.Utilities.Formula.TryEvaluate("max(1, 2)", new Dictionary<string, object>(), null, out var wholeMax, out _) && wholeMax is 2
            && Fn("nope(4)") == null && Fn("round(1, 2)") == null && Fn("max()") == null && Fn("abs(true)") == null && Fn("round(2") == null);

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
        Check("block list: headings in order, expanded at first", palette.Groups.Select(g => g.Title).SequenceEqual(new[] { "Control", "Twitch" }) && twitchGroup.ShowCards && twitchGroup.Heading == $"Twitch ({twitchGroup.Cards.Count}) ▾");
        var otherPalette = new BlockPaletteViewModel(layout, BlockGroup.Twitch);
        twitchGroup.HeadingPressedCommand.Execute(null);
        Check("block list: clicking a heading collapses it, in every list, and it's saved", !twitchGroup.ShowCards && twitchGroup.Heading == $"Twitch ({twitchGroup.Cards.Count}) ▸" && !otherPalette.Groups[0].ShowCards
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
    private static async Task AnyTypeBlocks()
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

        // growing lists reorder (an item dragged onto another goes before it, typed text and blocks alike)
        var parts = new ValueSlotList("Parts");
        foreach (var part in new[] { "a", "b", "c" }) parts.Items[^1].SetText(part);
        var movedBlock = new StringBuilderBlock();
        parts.Items[1].BlockSlot.TrySetBlock(movedBlock);
        string Parts() => string.Join(",", parts.Items.Select(i => i.Text == "" ? "_" : i.Text));
        Check("value list: an item moves before the one it's dropped on", parts.TryMoveItem(parts.Items[2], 0) && Parts() == "c,a,b,_");
        Check("value list: moved to the end it stays above the empty one, block and all", parts.TryMoveItem(parts.Items[2], 99) && Parts() == "c,a,b,_"
            && parts.TryMoveItem(parts.Items[1], 4) && Parts() == "c,b,a,_" && parts.Items[1].BlockSlot.Block == movedBlock);
        var pairs = new ValueSlotPairList("Rows", "Name…");
        pairs.Items[^1].Key.SetText("x");
        pairs.Items[^1].Key.SetText("y");
        Check("pair list: rows reorder too", pairs.TryMoveItem(pairs.Items[1], 0) && pairs.Items[0].Key.Text == "y" && pairs.Items[1].Key.Text == "x" && pairs.Items[2].IsEmpty);
        parts.Dispose();
        pairs.Dispose();

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
            var keysBinding = new BindableModel(); keysBinding.TryAddBlock(new KeysRecordBlock()); slot.Target.TryBind(keysBinding);
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
}
