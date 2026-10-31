using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Data.Model;



// every table (saved in Config/data.db by DataStore), and the rules for changing them. Everything that changes a table
// goes through these Try methods (the Data tab, blocks, chat commands, ...), which log why when they refuse, and raise
// an event per change (DataStore writes it, the Data tab redraws it). Built-in tables (variables) refuse changes to their
// columns or the table itself. Variables are the rows of the variables table: a name, a type and a value (kept as text,
// read as its type)
public class DataModel : ModelBase
{
    // constants



    public const string VariablesTable = "variables";
    public static readonly IReadOnlyList<ValueKind> ColumnKinds = [ValueKind.Text, ValueKind.Int, ValueKind.Double, ValueKind.Bool];

    // a table or column name: a letter or _ first, then letters, digits, _ (it's used in SQL, so nothing else)
    private static readonly Regex NamePattern = new(@"^[A-Za-z_][A-Za-z0-9_]{0,39}$");



    // private



    private readonly List<TableModel> _tables;



    // public



    public IReadOnlyList<TableModel> Tables => _tables;



    // events



    public event Action<TableModel>? TableAdded;
    public event Action<TableModel>? TableRemoved;
    public event Action<TableModel, string>? TableRenamed; // the table, its old name
    public event Action<TableModel, TableColumn>? ColumnAdded;
    public event Action<TableModel, TableColumn>? ColumnRemoved;
    public event Action<TableModel, TableColumn, string>? ColumnRenamed; // the column, its old name
    public event Action<TableModel, TableColumn>? ColumnKindChanged; // every value in it was converted too
    public event Action<TableModel, TableRow>? RowAdded;
    public event Action<TableModel, TableRow>? RowRemoved;
    public event Action<TableModel, TableRow, TableColumn>? ValueChanged;
    public event Action<TableModel, IReadOnlyList<(int Old, int New)>>? RowsRenumbered; // only the rows whose id changed, lowest first



    // constructor



    public DataModel()
    {
        _tables = [];
    }



    // loading (DataStore, at startup: no events, nothing is written back)



    public void Load(IEnumerable<TableModel> tables)
    {
        _tables.Clear();
        _tables.AddRange(tables);
    }



    // makes the built-in tables if they're missing (raising TableAdded / ColumnAdded so they're saved)
    public void EnsureBuiltInTables()
    {
        if (FindTable(VariablesTable) != null) return;

        var variables = new TableModel(VariablesTable, isBuiltIn: true);
        _tables.Insert(0, variables);
        TableAdded?.Invoke(variables);

        foreach (var column in new[] { new TableColumn("name", ValueKind.Text), new TableColumn("type", ValueKind.Text), new TableColumn("value", ValueKind.Text) })
        {
            variables.ColumnList.Add(column);
            ColumnAdded?.Invoke(variables, column);
        }
    }



    public static bool IsBuiltInName(string name) => string.Equals(name, VariablesTable, StringComparison.OrdinalIgnoreCase);



    // tables



    public TableModel? FindTable(string name) => _tables.Find(t => string.Equals(t.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));



    public bool TryCreateTable(string name)
    {
        name = name.Trim();
        if (!CheckNewName("create table", name, _tables.Select(t => t.Name))) return false;

        var table = new TableModel(name, isBuiltIn: false);
        _tables.Add(table);
        TableAdded?.Invoke(table);
        return true;
    }



    public bool TryDeleteTable(string name)
    {
        if (!TryFindTable("delete table", name, out var table)) return false;
        if (table.IsBuiltIn) return Refuse($"can't delete built-in table {table.Name}");

        _tables.Remove(table);
        TableRemoved?.Invoke(table);
        return true;
    }



    public bool TryRenameTable(string name, string newName)
    {
        newName = newName.Trim();
        if (!TryFindTable("rename table", name, out var table)) return false;
        if (table.IsBuiltIn) return Refuse($"can't rename built-in table {table.Name}");
        if (newName == table.Name) return true;
        if (!CheckNewName($"rename {table.Name}", newName, _tables.Where(t => t != table).Select(t => t.Name))) return false;

        var oldName = table.Name;
        table.Name = newName;
        TableRenamed?.Invoke(table, oldName);
        return true;
    }



    // columns (custom tables only)



    public bool TryAddColumn(string table, string name, ValueKind kind)
    {
        name = name.Trim();
        if (!TryFindCustomTable("add column", table, out var found)) return false;
        if (!ColumnKinds.Contains(kind)) return Refuse($"can't add column {name}, {kind} isn't a column type");
        if (!CheckNewName($"add column to {found.Name}", name, found.Columns.Select(c => c.Name).Append("id"))) return false;

        var column = new TableColumn(name, kind);
        found.ColumnList.Add(column);
        ColumnAdded?.Invoke(found, column);
        return true;
    }



    // onlyIfEmpty: refused while any row has a value in it (clearing a column's name only removes an empty column)
    public bool TryDeleteColumn(string table, string column, bool onlyIfEmpty = false)
    {
        if (!TryFindCustomTable("delete column", table, out var found) || !TryFindColumn(found, column, out var col)) return false;
        if (onlyIfEmpty && found.RowList.Any(r => r.Get(col.Name) is { } value && !(value is string text && text == "")))
            return Refuse($"can't remove column {col.Name}, it has values");

        found.ColumnList.Remove(col);
        foreach (var row in found.RowList) row.Remove(col.Name);
        ColumnRemoved?.Invoke(found, col);
        return true;
    }



    public bool TryRenameColumn(string table, string column, string newName)
    {
        newName = newName.Trim();
        if (!TryFindCustomTable("rename column", table, out var found) || !TryFindColumn(found, column, out var col)) return false;
        if (newName == col.Name) return true;
        if (!CheckNewName($"rename {found.Name}.{col.Name}", newName, found.Columns.Where(c => c != col).Select(c => c.Name).Append("id"))) return false;

        var oldName = col.Name;
        foreach (var row in found.RowList) row.Rename(oldName, newName);
        col.Name = newName;
        ColumnRenamed?.Invoke(found, col, oldName);
        return true;
    }



    // every value is converted to the new kind first, refused if any doesn't fit (nothing changes then)
    public bool TrySetColumnKind(string table, string column, ValueKind kind)
    {
        if (!TryFindCustomTable("change column type", table, out var found) || !TryFindColumn(found, column, out var col)) return false;
        if (!ColumnKinds.Contains(kind)) return Refuse($"can't change {col.Name}, {kind} isn't a column type");
        if (kind == col.Kind) return true;

        var converted = new List<(TableRow Row, object? Value)>();
        foreach (var row in found.RowList)
        {
            var value = row.Get(col.Name);
            if (value == null || (value is string empty && empty == "" && kind != ValueKind.Text))
            {
                converted.Add((row, null));
                continue;
            }

            if (!ValueTypes.TryConvert(value, col.Kind, kind, out var result, out _) && !ValueTypes.TryParse(ValueTypes.ToText(value), kind, out result))
                return Refuse($"can't make {found.Name}.{col.Name} {kind}, row {row.Id} has '{ValueTypes.ToText(value)}'");
            converted.Add((row, result));
        }

        col.Kind = kind;
        foreach (var (row, value) in converted) row.Set(col.Name, value);
        ColumnKindChanged?.Invoke(found, col);
        return true;
    }



    // rows



    // a new row at the end with empty cells (a variable gets a free name, type Text and an empty value)
    public bool TryAddRow(string table, out TableRow row)
    {
        row = null!;
        if (!TryFindTable("add row", table, out var found)) return false;

        row = new TableRow(found.NextId++);
        if (IsBuiltInName(found.Name))
        {
            var n = found.RowList.Count + 1;
            while (FindVariableRow($"variable{n}") != null) n++;
            row.Set("name", $"variable{n}");
            row.Set("type", nameof(ValueKind.Text));
            row.Set("value", "");
        }
        else
        {
            foreach (var column in found.ColumnList) row.Set(column.Name, column.Kind == ValueKind.Text ? "" : null);
        }

        found.RowList.Add(row);
        RowAdded?.Invoke(found, row);
        return true;
    }



    // closes the gaps deleted rows left: the rows keep their order and become 1, 2, 3, ... and the next new row comes after
    // them (e.g. quotes 4 and 7 become 1 and 2). Anything that kept an old id (a {rowId}, a quote number in chat) now
    // points somewhere else. Already 1, 2, 3, ... is nothing to do
    public bool TryRenumberRows(string table)
    {
        if (!TryFindTable("renumber rows", table, out var found)) return false;

        var changed = new List<(int Old, int New)>();
        var rows = found.RowList.OrderBy(r => r.Id).ToList();
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Id == i + 1) continue;
            changed.Add((rows[i].Id, i + 1));
            rows[i].Id = i + 1;
        }

        found.RowList.Clear();
        found.RowList.AddRange(rows);
        found.NextId = rows.Count + 1;

        if (changed.Count > 0) RowsRenumbered?.Invoke(found, changed);
        return true;
    }



    // onlyIfEmpty: does nothing (and isn't an error) while any of its cells has a value
    public bool TryDeleteRow(string table, int id, bool onlyIfEmpty = false)
    {
        if (!TryFindTable("delete row", table, out var found)) return false;
        var row = found.FindRow(id);
        if (row == null) return Refuse($"can't delete row {id} of {found.Name}, not found");
        if (onlyIfEmpty && found.ColumnList.Any(c => row.Get(c.Name) is { } value && !(value is string text && text == ""))) return true;

        found.RowList.Remove(row);
        RowRemoved?.Invoke(found, row);
        return true;
    }



    // values



    // value is typed text (from the Data tab, read as the column's kind; empty = an empty cell) or a value from a block
    // (converted to the column's kind). Variables also check their own rules (see SetVariableCell)
    public bool TrySetValue(string table, int id, string column, object? value, ValueKind? valueKind = null)
    {
        if (!TryFindTable("set value", table, out var found)) return false;
        var row = found.FindRow(id);
        if (row == null) return Refuse($"can't set {found.Name} row {id}, not found");
        if (!TryFindColumn(found, column, out var col)) return false;

        if (!TryToColumnKind(value, valueKind, col.Kind, out var converted))
            return Refuse($"can't set {found.Name}.{col.Name} to '{ValueTypes.ToText(value ?? "")}', not {ValueTypes.Describe(col.Kind)}");

        if (IsBuiltInName(found.Name)) return SetVariableCell(found, row, col, (string)(converted ?? ""));

        if (Equals(row.Get(col.Name), converted)) return true;
        row.Set(col.Name, converted);
        ValueChanged?.Invoke(found, row, col);
        return true;
    }



    // adds to a number cell (amount can be negative, an empty cell counts as 0), flips a true/false one (empty = false)
    public bool TryChangeValue(string table, int id, string column, double amount)
    {
        if (!TryFindTable("change value", table, out var found)) return false;
        var row = found.FindRow(id);
        if (row == null) return Refuse($"can't change {found.Name} row {id}, not found");
        if (!TryFindColumn(found, column, out var col)) return false;

        var current = row.Get(col.Name);
        object result;
        switch (col.Kind)
        {
            case ValueKind.Int when amount != Math.Truncate(amount):
                return Refuse($"can't add {amount} to {found.Name}.{col.Name}, it's whole numbers");
            case ValueKind.Int:
                var whole = (long)(current as int? ?? 0) + (long)amount;
                if (whole is > int.MaxValue or < int.MinValue) return Refuse($"can't add to {found.Name}.{col.Name}, too big");
                result = (int)whole;
                break;
            case ValueKind.Double:
                result = (current as double? ?? 0) + amount;
                break;
            case ValueKind.Bool:
                result = !(current as bool? ?? false);
                break;
            default:
                return Refuse($"can't add to {found.Name}.{col.Name}, it's text");
        }

        return TrySetValue(found.Name, id, col.Name, result, col.Kind);
    }



    // variables



    public List<string> VariableNames() =>
        FindTable(VariablesTable)?.RowList.Select(r => r.Get("name") as string ?? "").Where(n => n != "").ToList() ?? [];



    // the variable's type and value (read as that type)
    public bool TryGetVariable(string name, out ValueKind kind, out object value)
    {
        kind = ValueKind.Text;
        value = "";
        var row = FindVariableRow(name);
        if (row == null) return Refuse($"no variable '{name.Trim()}'");

        kind = KindOf(row);
        var text = row.Get("value") as string ?? "";
        if (!ValueTypes.TryParse(text, kind, out value)) value = DefaultValue(kind); // only a hand-edited file could get here
        return true;
    }



    public ValueKind? VariableKind(string name) => FindVariableRow(name) is { } row ? KindOf(row) : null;



    // a value of any kind, converted to the variable's type (refused if it doesn't fit)
    public bool TrySetVariable(string name, object value, ValueKind? valueKind = null)
    {
        var row = FindVariableRow(name);
        if (row == null) return Refuse($"no variable '{name.Trim()}'");

        var kind = KindOf(row);
        if (!TryToKind(value, valueKind, kind, out var converted))
            return Refuse($"can't set {name.Trim()} to '{ValueTypes.ToText(value)}', not {ValueTypes.Describe(kind)}");

        return TrySetValue(VariablesTable, row.Id, "value", ValueTypes.ToText(converted));
    }



    // adds to a number (amount can be negative), flips a true/false; text can't change this way
    public bool TryChangeVariable(string name, double amount, out object result)
    {
        result = "";
        if (!TryGetVariable(name, out var kind, out var value)) return false;

        switch (kind)
        {
            case ValueKind.Int when amount != Math.Truncate(amount):
                return Refuse($"can't add {amount} to {name.Trim()}, it's a whole number");
            case ValueKind.Int:
                var whole = (long)(int)value + (long)amount;
                if (whole is > int.MaxValue or < int.MinValue) return Refuse($"can't add to {name.Trim()}, too big");
                result = (int)whole;
                break;
            case ValueKind.Double:
                result = (double)value + amount;
                break;
            case ValueKind.Bool:
                result = !(bool)value;
                break;
            default:
                return Refuse($"can't add to {name.Trim()}, it's text");
        }

        return TrySetVariable(name, result, kind);
    }



    // helpers



    // a variables cell: a valid unique name, one of the four types (its value has to fit, an empty one takes the type's
    // default), or a value that fits the type
    private bool SetVariableCell(TableModel table, TableRow row, TableColumn column, string text)
    {
        var changes = new List<(TableColumn Column, string Value)>();

        switch (column.Name.ToLowerInvariant())
        {
            case "name":
                text = text.Trim();
                if (!TriggerContext.IsValidName(text)) return Refuse($"can't name a variable '{text}', use letters, digits, _");
                if (FindVariableRow(text) is { } other && other != row) return Refuse($"can't name a variable '{text}', already exists");
                changes.Add((column, text));
                break;

            case "type":
                if (!Enum.TryParse<ValueKind>(text.Trim(), ignoreCase: true, out var kind) || !ColumnKinds.Contains(kind))
                    return Refuse($"can't make {row.Get("name")} '{text}', use Text, Int, Double or Bool");
                var current = row.Get("value") as string ?? "";
                if (current.Trim() == "" && kind != ValueKind.Text) changes.Add((table.FindColumn("value")!, ValueTypes.ToText(DefaultValue(kind))));
                else if (!ValueTypes.TryParse(current, kind, out _)) return Refuse($"can't make {row.Get("name")} {kind}, its value '{current}' isn't {ValueTypes.Describe(kind)}");
                changes.Insert(0, (column, kind.ToString()));
                break;

            case "value":
                var type = KindOf(row);
                if (!ValueTypes.TryParse(text, type, out var parsed)) return Refuse($"can't set {row.Get("name")} to '{text}', not {ValueTypes.Describe(type)}");
                changes.Add((column, ValueTypes.ToText(parsed)));
                break;
        }

        foreach (var (changed, value) in changes)
        {
            if (row.Get(changed.Name) as string == value) continue;
            row.Set(changed.Name, value);
            ValueChanged?.Invoke(table, row, changed);
        }
        return true;
    }



    private TableRow? FindVariableRow(string name) =>
        FindTable(VariablesTable)?.RowList.Find(r => string.Equals(r.Get("name") as string, name.Trim(), StringComparison.OrdinalIgnoreCase));



    private static ValueKind KindOf(TableRow variable) =>
        Enum.TryParse<ValueKind>(variable.Get("type") as string, ignoreCase: true, out var kind) && ColumnKinds.Contains(kind) ? kind : ValueKind.Text;



    private static object DefaultValue(ValueKind kind) => kind switch
    {
        ValueKind.Int => 0,
        ValueKind.Double => 0.0,
        ValueKind.Bool => false,
        _ => "",
    };



    // typed text is read as the kind (empty = an empty cell, except for text), a value from a block is converted
    private static bool TryToColumnKind(object? value, ValueKind? valueKind, ValueKind kind, out object? result)
    {
        result = null;
        if (value == null) return true;
        if (value is string text && valueKind == null && text.Trim() == "" && kind != ValueKind.Text) return true;

        var ok = TryToKind(value, valueKind, kind, out var converted);
        result = converted;
        return ok;
    }



    private static bool TryToKind(object value, ValueKind? valueKind, ValueKind kind, out object result)
    {
        if (valueKind is { } from && from != kind) return ValueTypes.TryConvert(value, from, kind, out result, out _);
        if (value is string text) return ValueTypes.TryParse(text, kind, out result);

        result = value;
        return ValueTypes.KindOf(value.GetType()) == kind || ValueTypes.TryConvert(value, ValueTypes.KindOf(value.GetType()), kind, out result, out _);
    }



    private bool CheckNewName(string what, string name, IEnumerable<string> taken)
    {
        if (!NamePattern.IsMatch(name)) return Refuse($"can't {what} '{name}', use letters, digits, _ (a letter first)");
        if (name.StartsWith("sqlite_", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "id", StringComparison.OrdinalIgnoreCase) && what.Contains("column"))
            return Refuse($"can't {what} '{name}', that name is taken");
        if (taken.Any(t => string.Equals(t, name, StringComparison.OrdinalIgnoreCase))) return Refuse($"can't {what} '{name}', already exists");
        return true;
    }



    private bool TryFindTable(string what, string name, out TableModel table)
    {
        table = FindTable(name)!;
        return table != null || Refuse($"can't {what}, no table '{name.Trim()}'");
    }



    private bool TryFindCustomTable(string what, string name, out TableModel table)
    {
        if (!TryFindTable(what, name, out table)) return false;
        return !table.IsBuiltIn || Refuse($"can't {what}, {table.Name} is built-in");
    }



    private static bool TryFindColumn(TableModel table, string name, out TableColumn column)
    {
        column = table.FindColumn(name)!;
        return column != null || Refuse($"{table.Name} has no column '{name.Trim()}'");
    }



    private static bool Refuse(string message)
    {
        AppLog.WriteError(message);
        return false;
    }



    // disposal



    public override void Dispose() { }
}
