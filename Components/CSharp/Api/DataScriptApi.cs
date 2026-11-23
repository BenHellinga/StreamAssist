using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.CSharp.Background;
using StreamAssist.Components.Data.Model;
using StreamAssist.Core.Utilities;
using StreamAssist.Scripting;
using StreamAssist.Shell;

namespace StreamAssist.Components.CSharp.Api;



// a table's name and columns as they were when asked (for blocks, which run on another thread than the model)
public sealed record TableShape(string Name, IReadOnlyList<(string Name, ValueKind Kind)> Columns)
{
    public (string Name, ValueKind Kind)? FindColumn(string name)
    {
        var wanted = name.Trim();
        foreach (var column in Columns)
            if (string.Equals(column.Name, wanted, StringComparison.OrdinalIgnoreCase)) return column;
        return null;
    }
}



// IData: the data model checks and logs its own failures (so those throw already logged); lookups that only read throw
// a plain error for the caller to log. Each call first waits while another run has the table or variable locked
// (ScriptLocks), then does its work on the UI thread
public sealed class DataScriptApi(TriggerContext context) : ScriptApiPart(context), IData
{
    private static DataModel Data => Need(GlobalContext.DataModel, "data");



    // variables



    public object GetVariable(string name) => OnVariable(name, () =>
        Data.TryGetVariable(name, out _, out var value) ? value : throw Logged($"no variable '{name}'"));



    public T GetVariable<T>(string name) => OnVariable(name, () =>
    {
        if (!Data.TryGetVariable(name, out var kind, out var value)) throw Logged($"no variable '{name}'");
        return (T)ConvertTo(value, kind, typeof(T));
    });



    public void SetVariable(string name, object value) => SetVariable(name, Normalize(value), KindOf(value));



    // for blocks: the kind their slot gave (null: plain text)
    public void SetVariable(string name, object value, ValueKind? kind) => OnVariable(name, () =>
        Data.TrySetVariable(name, value, kind) ? true : throw Logged("variable not set"));



    // for blocks: the value as the slot's type
    public object GetVariable(string name, Type type) => OnVariable(name, () =>
    {
        if (!Data.TryGetVariable(name, out var kind, out var value)) throw Logged($"no variable '{name}'");
        return ConvertTo(value, kind, type);
    });



    public object ChangeVariable(string name, double amount) => OnVariable(name, () =>
        Data.TryChangeVariable(name, amount, out var result) ? result : throw Logged("variable not changed"));



    public bool HasVariable(string name) => UiThread.Run(() => Data.VariableKind(name) != null);



    // tables



    public IReadOnlyList<DataRow> GetRows(string table, string query = "") => OnTable(table, () =>
    {
        var found = Table(table);
        var parsed = RowQuery.TryParse(query, found, out var error) ?? throw Error($"query '{query}' {error}");
        return (IReadOnlyList<DataRow>)found.Rows.Where(r => parsed.Matches(found, r)).Select(r => RowOf(found, r)).ToList();
    });



    public DataRow? GetRow(string table, int id) => OnTable(table, () =>
    {
        var found = Table(table);
        return found.FindRow(id) is { } row ? RowOf(found, row) : null;
    });



    public int CountRows(string table) => OnTable(table, () => Table(table).Rows.Count);



    public int RandomRow(string table) => OnTable(table, () =>
    {
        var found = Table(table);
        return found.Rows.Count == 0 ? 0 : found.Rows[Random.Shared.Next(found.Rows.Count)].Id;
    });



    public int FindRow(string table, string column, string value) => OnTable(table, () =>
    {
        var found = Table(table);
        var col = found.FindColumn(column) ?? throw Error($"{found.Name} has no column '{column}'");
        var wanted = value.Trim();
        var row = found.Rows.FirstOrDefault(r => string.Equals(ValueTypes.ToText(r.Get(col.Name) ?? "").Trim(), wanted, StringComparison.OrdinalIgnoreCase));
        return row?.Id ?? 0;
    });



    public int FindOrAddRow(string table, string column, string value) => OnTable(table, () =>
        Data.TryFindOrAddRow(table, column, value, out var row) ? row.Id : throw Logged("row not found or added"));



    public int AddRow(string table) => OnTable(table, () =>
        Data.TryAddRow(table, out var row) ? row.Id : throw Logged("row not added"));



    public void DeleteRow(string table, int id) => OnTable(table, () =>
        Data.TryDeleteRow(table, id) ? true : throw Logged("row not deleted"));



    public object? GetValue(string table, string row, string column) => OnTable(table, () =>
    {
        var data = Data;
        var found = Table(table);
        var col = found.FindColumn(column) ?? throw Error($"{found.Name} has no column '{column}'");
        if (!data.TryFindRow(found.Name, row, addIfMissing: false, out var match)) throw Logged("row not found");
        return match?.Get(col.Name); // no row with that value reads like an empty cell
    });



    public void SetValue(string table, string row, string column, object? value) =>
        SetValue(table, row, column, value == null ? null : Normalize(value), value == null ? null : KindOf(value));



    // for blocks: the kind their slot gave
    public void SetValue(string table, string row, string column, object? value, ValueKind? kind) => OnTable(table, () =>
    {
        var data = Data;
        if (!data.TryFindRow(table, row, addIfMissing: true, out var match)) throw Logged("row not found");
        return data.TrySetValue(table, match!.Id, column, value, kind) ? true : throw Logged("value not set");
    });



    public void ChangeValue(string table, string row, string column, double amount) => OnTable(table, () =>
    {
        var data = Data;
        if (!data.TryFindRow(table, row, addIfMissing: true, out var match)) throw Logged("row not found");
        return data.TryChangeValue(table, match!.Id, column, amount) ? true : throw Logged("value not changed");
    });



    // for blocks: a table's name and columns
    public static TableShape Shape(string table) => UiThread.Run(() =>
    {
        var found = Table(table);
        return new TableShape(found.Name, found.Columns.Select(c => (c.Name, c.Kind)).ToList());
    });



    // locks



    public Task<HeldLock> LockTableAsync(string table)
    {
        var name = UiThread.Run(() => Table(table).Name);
        return ScriptLocks.AcquireAsync(TableKey(name), $"table {name}");
    }



    public Task<HeldLock> LockVariableAsync(string name)
    {
        if (!HasVariable(name)) throw Logged($"no variable '{name}'");
        return ScriptLocks.AcquireAsync(VariableKey(name), $"variable {name.Trim()}");
    }



    // what a run waits for before it can use a table or variable another run has locked
    public static string TableKey(string table) => "table:" + table.Trim().ToLowerInvariant();

    public static string VariableKey(string name) => "variable:" + name.Trim().ToLowerInvariant();



    // cooldowns



    public bool TryUseCooldown(string label, double seconds) => TryUseCooldown(label, seconds, out _);



    public bool TryUseCooldown(string label, double seconds, out double secondsLeft)
    {
        var name = label.Trim();
        if (name == "") throw Error("needs a label");
        if (seconds < 0) throw Error($"can't cool down for {seconds} seconds");

        var (used, left) = UiThread.Run(() => (Data.UseCooldown(name, seconds, DateTime.Now, out var l), l));
        secondsLeft = left;
        return used;
    }



    // helpers



    // waits while another run has it locked, then does the work on the UI thread
    private T OnVariable<T>(string name, Func<T> work)
    {
        ScriptLocks.WaitUntilFree(VariableKey(name), $"variable {name.Trim()}", Token);
        return UiThread.Run(work);
    }



    private T OnTable<T>(string table, Func<T> work)
    {
        ScriptLocks.WaitUntilFree(TableKey(table), $"table {table.Trim()}", Token);
        return UiThread.Run(work);
    }



    // UI thread only
    private static TableModel Table(string name) => Data.FindTable(name) ?? throw Error($"no table '{name}'");



    private static DataRow RowOf(TableModel table, TableRow row) =>
        new(row.Id, table.Columns.ToDictionary(c => c.Name, c => row.Get(c.Name), StringComparer.OrdinalIgnoreCase));



    // the kinds blocks use, from what a script passes (other numbers count as decimals, anything else as text)
    private static ValueKind KindOf(object value) => value switch
    {
        int => ValueKind.Int,
        double or float or decimal or long or short or byte => value is int ? ValueKind.Int : ValueKind.Double,
        bool => ValueKind.Bool,
        char => ValueKind.Char,
        _ => ValueKind.Text,
    };



    private static object Normalize(object value) => value switch
    {
        int or double or bool or char or string => value,
        float or decimal or long or short or byte => System.Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };



    // a value as T, the way a value slot of that type would take it
    public static object ConvertTo(object value, ValueKind kind, Type type)
    {
        if (type == typeof(object)) return value;
        var wanted = ValueTypes.KindOf(type);
        if (!ValueTypes.TryConvert(value, kind, wanted, out var result, out var error)) throw Error(error);
        return result;
    }
}
