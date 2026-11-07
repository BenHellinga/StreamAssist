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



// engine checks: config files, the SQLite data store and asset paths (temporary folders, never the real Config/)
internal static partial class EngineChecks
{
    // ---- config files: a broken one is never overwritten (in a temporary folder, never the real Config/) ----
    private static void ConfigFiles()
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
    private static async Task DataTablesAndVariables()
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

        // a variable dock: rows name variables and edit their values, and follow scripts' changes
        var varDock = StreamAssist.Components.Dashboard.Model.DockModel.Create("Vars", StreamAssist.Components.Dashboard.Model.DockKind.Variable);
        varDock.Variables.Rows[^1].SetName("deaths");
        varDock.Variables.Rows[^1].SetName("streamWord");
        var varView = new StreamAssist.Components.Dashboard.UI.VariableDockViewModel(varDock, data);
        var deathsRow = varView.Rows[0];
        var wordRow = varView.Rows[1];
        Check("variable dock: shows a variable's value, a missing one grey and empty", deathsRow.ValueInput == "10" && !deathsRow.IsMissing && wordRow.IsMissing && wordRow.ValueInput == ""
            && !varView.Rows[2].ShowValue);
        wordRow.ValueInput = "goose\nparty";
        wordRow.Commit();
        Check("variable dock: typing a value makes a Text variable (several lines too)", data.GetVariableText("streamWord") == "goose\nparty" && !wordRow.IsMissing);
        data.TrySetVariable("deaths", 11);
        Check("variable dock: a script's change shows", deathsRow.ValueInput == "11");
        deathsRow.ValueInput = "lots";
        deathsRow.Commit();
        Check("variable dock: a value that doesn't fit is refused and put back", deathsRow.ValueInput == "11" && data.GetVariableText("deaths") == "11");
        varView.Dispose();
        varDock.Dispose();
        data.TrySetVariable("deaths", 10); // back as the checks below expect them
        data.TryDeleteRow("variables", data.FindTable("variables")!.Rows.First(r => r.Get("name") is "streamWord").Id);
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
            var slots = block.Slots.OfType<ValueSlot>().Where(v => v is not MenuSlot && v.Label != "Table").ToList();
            for (int i = 0; i < texts.Length; i++) slots[i].SetText(texts[i]);
            return block;
        }

        Check("data blocks: set variable", await Do(With(new SetVariableAction().Pick("Variable", "deaths"), "5")) && reloaded.TryGetVariable("deaths", out _, out var five) && five is 5);
        Check("data blocks: a value that doesn't fit fails and stops", !await Do(With(new SetVariableAction().Pick("Variable", "deaths"), "lots")));
        Check("data blocks: change variable", await Do(new ChangeVariableAction().Pick("Variable", "deaths")) && reloaded.TryGetVariable("deaths", out _, out var six) && six is 6);
        var getDeaths = new GetVariableBlock().Pick("Variable", "deaths");
        Check("data blocks: get variable picks the variable's type", getDeaths.Kind == ValueKind.Int && await getDeaths.EvaluateObjectAsync(dctx) is 6);
        Check("data blocks: an unknown variable fails", !await Do(new ChangeVariableAction().Pick("Variable", "nope")));
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

        // find or add a row, and rows as text (a leaderboard)
        reloaded.TryCreateTable("spent");
        reloaded.TryAddColumn("spent", "user", ValueKind.Text);
        reloaded.TryAddColumn("spent", "total", ValueKind.Int);
        async Task<bool> Spend(string user, string amount)
        {
            var spendContext = new TriggerContext(TriggerPhase.Once, default);
            var find = new FindOrAddTableRowAction { Table = "spent" };
            ((ValueSlot)find.Slots[1]).SetText("user");
            ((ValueSlot)find.Slots[2]).SetText(user);
            var change = With(new ChangeTableValueAction { Table = "spent" }, "{rowId}", "total", amount);
            var list = new ListBlock();
            ((BlockSlotList)list.Slots[0]).TryInsertBlock(find, 0);
            ((BlockSlotList)list.Slots[0]).TryInsertBlock(change, 1);
            await list.SafeRunAsync(spendContext);
            return spendContext.Parameters.ContainsKey("rowId");
        }
        Check("find or add row: adds a row the first time, finds it after (case ignored)", await Spend("budder", "10") && await Spend("BUDDER", "5") && await Spend("amy", "40") && await Spend("cal", "1")
            && reloaded.FindTable("spent")!.Rows.Count == 3 && reloaded.FindTable("spent")!.Rows[0].Get("total") is 15);
        Check("row by value: Change Table Value adds the row if there's none, Get Table Value reads it (or 0 for none)",
            await Do(With(new ChangeTableValueAction { Table = "spent" }, "user=dave", "total", "7")) && await Do(With(new ChangeTableValueAction { Table = "spent" }, "user=DAVE", "total", "1"))
            && await With(new GetTableValueBlock { Table = "spent", As = "Int" }, "user=dave", "total").EvaluateObjectAsync(dctx) is 8
            && await With(new GetTableValueBlock { Table = "spent", As = "Int" }, "user=nobody", "total").EvaluateObjectAsync(dctx) is 0
            && !await Do(With(new ChangeTableValueAction { Table = "spent" }, "dave", "total", "1")));
        reloaded.TryDeleteRow("spent", reloaded.FindTable("spent")!.Rows.Single(r => r.Get("user") is "dave").Id);
        var top = new TableRowsBlock { Table = "spent" };
        ((ValueSlot)top.Slots[4]).SetText("total");
        ((ValueSlot)top.Slots[5]).SetText("2");
        ((ValueSlot)top.Slots[6]).SetText("{user}: {total}");
        Check("table rows: highest first, the first Count, each row's columns as {names}", await top.EvaluateAsync(dctx) == "amy: 40, budder: 15");
        top.Pick("Order", "Lowest first");
        ((ValueSlot)top.Slots[3]).SetText("total>1");
        Check("table rows: lowest first, filtered by a query", await top.EvaluateAsync(dctx) == "budder: 15, amy: 40");
        ((ValueSlot)top.Slots[3]).SetText("user=nobody");
        ((ValueSlot)top.Slots[3]).SetText("");
        reloaded.TrySetValue("spent", 1, "total", "1000090");
        top.Pick("Numbers", "1,000s");
        top.Pick("Order", "Highest first");
        Check("table rows: numbers with thousands separators", await top.EvaluateAsync(dctx) == "budder: 1,000,090, amy: 40");
        ((ValueSlot)top.Slots[3]).SetText("user=nobody");
        Check("table rows: no rows is empty text", await top.EvaluateAsync(dctx) == "" && !dctx.Parameters.ContainsKey("user"));
        Check("data blocks: a missing table fails", !await Do(new AddTableRowAction { Table = "nope" }));
        Check("data blocks: under the Variables and Tables headings, saved and loaded", new SetVariableAction().Group == BlockGroup.Variables && new GetVariableBlock().Group == BlockGroup.Variables && new CountTableRowsBlock().Group == BlockGroup.Tables
            && BlockResolver.DeepCopy(new GetVariableBlock().Pick("Variable", "deaths")) is GetVariableBlock { Kind: ValueKind.Int } copiedVariable && copiedVariable.Picked("Variable") == "deaths");

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
    private static void AssetPaths()
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
}
