using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Data.Model;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Data.Background;



// keeps Config/data.db (SQLite) the same as DataModel: loads every table into it at startup, then writes each change the
// moment the model raises it (every change is its own small transaction, so a crash can't leave it half-written).
// The only code that talks SQL. Each table has an "id INTEGER PRIMARY KEY" plus its columns, declared as Text, Int,
// Double or Bool (true/false stored as 1/0). If the database can't be opened, the model works for this session only
public class DataStore : IDisposable
{
    // private



    private readonly DataModel _model;
    private readonly SqliteConnection? _connection;



    // constructor



    public DataStore(DataModel model, string path)
    {
        _model = model;
        _connection = Open(path);

        if (_connection != null)
        {
            try
            {
                _model.Load(LoadTables(_connection));
            }
            catch (SqliteException ex)
            {
                AppLog.WriteError($"can't read {Path.GetFileName(path)} ({ex.Message})");
            }
        }

        SubscribeToModel();
        _model.EnsureBuiltInTables();
    }



    // model callbacks



    private void OnTableAdded(TableModel table) => Run($"CREATE TABLE {Q(table.Name)} (id INTEGER PRIMARY KEY)");



    private void OnTableRemoved(TableModel table) => Run($"DROP TABLE {Q(table.Name)}");



    private void OnTableRenamed(TableModel table, string oldName) => Run($"ALTER TABLE {Q(oldName)} RENAME TO {Q(table.Name)}");



    private void OnColumnAdded(TableModel table, TableColumn column) => Run($"ALTER TABLE {Q(table.Name)} ADD COLUMN {Q(column.Name)} {column.Kind}");



    private void OnColumnRemoved(TableModel table, TableColumn column) => Run($"ALTER TABLE {Q(table.Name)} DROP COLUMN {Q(column.Name)}");



    private void OnColumnRenamed(TableModel table, TableColumn column, string oldName) =>
        Run($"ALTER TABLE {Q(table.Name)} RENAME COLUMN {Q(oldName)} TO {Q(column.Name)}");



    // SQLite can't change a column's declared type, so the table is written again from the model (values already converted)
    private void OnColumnKindChanged(TableModel table, TableColumn column) => InTransaction(connection =>
    {
        Execute(connection, $"DROP TABLE {Q(table.Name)}");
        Execute(connection, CreateSql(table));
        foreach (var row in table.Rows) Insert(connection, table, row);
    });



    private void OnRowAdded(TableModel table, TableRow row) => InTransaction(connection => Insert(connection, table, row));



    private void OnRowRemoved(TableModel table, TableRow row) => Run($"DELETE FROM {Q(table.Name)} WHERE id = {row.Id}");



    // lowest first: each new id is at most the old one and every lower id has already moved, so none collide
    private void OnRowsRenumbered(TableModel table, IReadOnlyList<(int Old, int New)> changed) => InTransaction(connection =>
    {
        foreach (var (old, @new) in changed)
            Execute(connection, $"UPDATE {Q(table.Name)} SET id = {@new} WHERE id = {old}");
    });



    private void OnValueChanged(TableModel table, TableRow row, TableColumn column) => InTransaction(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"UPDATE {Q(table.Name)} SET {Q(column.Name)} = $value WHERE id = $id";
        command.Parameters.AddWithValue("$value", ToDb(row.Get(column.Name)));
        command.Parameters.AddWithValue("$id", row.Id);
        command.ExecuteNonQuery();
    });



    // loading



    private static SqliteConnection? Open(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
            connection.Open();
            return connection;
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            AppLog.WriteError($"can't open {Path.GetFileName(path)}, data isn't saved ({ex.Message})");
            return null;
        }
    }



    private static List<TableModel> LoadTables(SqliteConnection connection)
    {
        var names = new List<string>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY rowid";
            using var reader = command.ExecuteReader();
            while (reader.Read()) names.Add(reader.GetString(0));
        }

        var tables = new List<TableModel>();
        foreach (var name in names)
        {
            var table = new TableModel(name, DataModel.IsBuiltInName(name));

            using (var command = connection.CreateCommand())
            {
                command.CommandText = $"PRAGMA table_info({Q(name)})";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var column = reader.GetString(1);
                    if (string.Equals(column, "id", StringComparison.OrdinalIgnoreCase)) continue;
                    var kind = Enum.TryParse<ValueKind>(reader.GetString(2), ignoreCase: true, out var parsed) && DataModel.ColumnKinds.Contains(parsed) ? parsed : ValueKind.Text;
                    table.ColumnList.Add(new TableColumn(column, kind));
                }
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = $"SELECT * FROM {Q(name)} ORDER BY id";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var row = new TableRow(Convert.ToInt32(reader["id"], CultureInfo.InvariantCulture));
                    foreach (var column in table.ColumnList)
                        row.Set(column.Name, FromDb(reader[column.Name], column.Kind));

                    table.RowList.Add(row);
                    table.NextId = Math.Max(table.NextId, row.Id + 1);
                }
            }

            tables.Add(table);
        }

        return tables;
    }



    // helpers



    private void Run(string sql) => InTransaction(connection => Execute(connection, sql));



    // runs the work as one transaction, logging (not throwing) if it fails: the model keeps the change for this session
    private void InTransaction(Action<SqliteConnection> work)
    {
        if (_connection == null) return;

        try
        {
            using var transaction = _connection.BeginTransaction();
            work(_connection);
            transaction.Commit();
        }
        catch (SqliteException ex)
        {
            AppLog.WriteError($"can't save data ({ex.Message})");
        }
    }



    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }



    private static void Insert(SqliteConnection connection, TableModel table, TableRow row)
    {
        using var command = connection.CreateCommand();
        var columns = new[] { "id" }.Concat(table.Columns.Select(c => Q(c.Name)));
        var values = new[] { "$id" }.Concat(table.Columns.Select((_, i) => $"$v{i}"));
        command.CommandText = $"INSERT INTO {Q(table.Name)} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)})";
        command.Parameters.AddWithValue("$id", row.Id);
        for (int i = 0; i < table.Columns.Count; i++) command.Parameters.AddWithValue($"$v{i}", ToDb(row.Get(table.Columns[i].Name)));
        command.ExecuteNonQuery();
    }



    private static string CreateSql(TableModel table) =>
        $"CREATE TABLE {Q(table.Name)} (id INTEGER PRIMARY KEY{string.Concat(table.Columns.Select(c => $", {Q(c.Name)} {c.Kind}"))})";



    // a name quoted for SQL (names are already letters, digits and _ only)
    private static string Q(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";



    private static object ToDb(object? value) => value switch
    {
        null => DBNull.Value,
        bool flag => flag ? 1 : 0,
        _ => value,
    };



    private static object? FromDb(object value, ValueKind kind)
    {
        if (value is DBNull) return null;

        try
        {
            return kind switch
            {
                ValueKind.Int => Convert.ToInt32(value, CultureInfo.InvariantCulture),
                ValueKind.Double => Convert.ToDouble(value, CultureInfo.InvariantCulture),
                ValueKind.Bool => Convert.ToInt64(value, CultureInfo.InvariantCulture) != 0,
                _ => Convert.ToString(value, CultureInfo.InvariantCulture),
            };
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            return null; // edited outside the app to something that doesn't fit: an empty cell
        }
    }



    // subscriptions



    private void SubscribeToModel()
    {
        _model.TableAdded += OnTableAdded;
        _model.TableRemoved += OnTableRemoved;
        _model.TableRenamed += OnTableRenamed;
        _model.ColumnAdded += OnColumnAdded;
        _model.ColumnRemoved += OnColumnRemoved;
        _model.ColumnRenamed += OnColumnRenamed;
        _model.ColumnKindChanged += OnColumnKindChanged;
        _model.RowAdded += OnRowAdded;
        _model.RowsRenumbered += OnRowsRenumbered;
        _model.RowRemoved += OnRowRemoved;
        _model.ValueChanged += OnValueChanged;
    }



    private void UnsubscribeFromModel()
    {
        _model.TableAdded -= OnTableAdded;
        _model.TableRemoved -= OnTableRemoved;
        _model.TableRenamed -= OnTableRenamed;
        _model.ColumnAdded -= OnColumnAdded;
        _model.ColumnRemoved -= OnColumnRemoved;
        _model.ColumnRenamed -= OnColumnRenamed;
        _model.ColumnKindChanged -= OnColumnKindChanged;
        _model.RowAdded -= OnRowAdded;
        _model.RowsRenumbered -= OnRowsRenumbered;
        _model.RowRemoved -= OnRowRemoved;
        _model.ValueChanged -= OnValueChanged;
    }



    // disposal



    public void Dispose()
    {
        UnsubscribeFromModel();
        _connection?.Dispose();
    }
}
