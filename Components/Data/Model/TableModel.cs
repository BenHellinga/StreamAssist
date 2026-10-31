using System.Collections.Generic;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Data.Model;



// one column of a table: its name and the kind of value its cells hold (Text, Int, Double or Bool)
public sealed class TableColumn(string name, ValueKind kind)
{
    public string Name { get; internal set; } = name;
    public ValueKind Kind { get; internal set; } = kind;
}



// one row: its id (1, 2, 3, ... never reused, like SQLite's, unless the table is renumbered) and a value per column (null = empty cell)
public sealed class TableRow(int id)
{
    // private



    private readonly Dictionary<string, object?> _values = new(System.StringComparer.OrdinalIgnoreCase);



    // public



    public int Id { get; internal set; } = id;



    // public api



    public object? Get(string column) => _values.TryGetValue(column, out var value) ? value : null;



    // only DataModel (and loading) changes values, everything else goes through its Try methods
    internal void Set(string column, object? value) => _values[column] = value;



    internal void Remove(string column) => _values.Remove(column);



    internal void Rename(string column, string newName)
    {
        var value = Get(column);
        _values.Remove(column);
        _values[newName] = value;
    }
}



// a table: its columns (besides the id every table has) and rows, top to bottom by id. Built-in tables belong to the
// app (e.g. variables): their values and rows can change, their columns and the table itself can't
public sealed class TableModel(string name, bool isBuiltIn)
{
    // private



    internal readonly List<TableColumn> ColumnList = [];
    internal readonly List<TableRow> RowList = [];



    // public



    public string Name { get; internal set; } = name;
    public bool IsBuiltIn { get; } = isBuiltIn;
    public IReadOnlyList<TableColumn> Columns => ColumnList;
    public IReadOnlyList<TableRow> Rows => RowList;
    public int NextId { get; internal set; } = 1;



    // public api



    public TableColumn? FindColumn(string name) => ColumnList.Find(c => string.Equals(c.Name, name.Trim(), System.StringComparison.OrdinalIgnoreCase));



    public TableRow? FindRow(int id) => RowList.Find(r => r.Id == id);
}
