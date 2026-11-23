using System.Collections.Generic;
using System.Threading.Tasks;

namespace StreamAssist.Scripting;



/// <summary>
/// The Data tab: variables, tables and cooldowns, kept after the app closes. Values are <c>string</c>, <c>int</c>,
/// <c>double</c>, <c>bool</c> or <c>char</c>, as the variable or column is set. A row is its id, or column=value
/// (e.g. <c>"user=" + Parameters["userLogin"]</c>). Names that don't exist are errors (logged, and a
/// <see cref="StreamAssistException"/> is thrown).
/// </summary>
public interface IData
{
    // variables



    /// <summary>A variable's value, as its type.</summary>
    object GetVariable(string name);

    /// <summary>A variable's value converted to T (string, int, double, bool or char), e.g. <c>GetVariable&lt;int&gt;("deaths")</c>.</summary>
    T GetVariable<T>(string name);

    /// <summary>Sets a variable; the value is converted to its type (an error if it doesn't fit).</summary>
    void SetVariable(string name, object value);

    /// <summary>Adds to a number variable (negative takes away), or flips a true/false one, in one step; gives the new value.</summary>
    object ChangeVariable(string name, double amount);

    /// <summary>Whether there's a variable with this name.</summary>
    bool HasVariable(string name);



    // tables



    /// <summary>Every row a query picks, like the Data tab's query box (empty: every row), in id order.</summary>
    IReadOnlyList<DataRow> GetRows(string table, string query = "");

    /// <summary>A row by its id, null if there's none.</summary>
    DataRow? GetRow(string table, int id);

    /// <summary>How many rows a table has.</summary>
    int CountRows(string table);

    /// <summary>The id of a row picked at random, 0 if the table is empty.</summary>
    int RandomRow(string table);

    /// <summary>The id of the first row whose column has this value (as text, case ignored), 0 if there's none.</summary>
    int FindRow(string table, string column, string value);

    /// <summary>The first row whose column has this value, or a new row with it; gives its id.</summary>
    int FindOrAddRow(string table, string column, string value);

    /// <summary>Adds an empty row at the end and gives its id.</summary>
    int AddRow(string table);

    /// <summary>Deletes a row by its id.</summary>
    void DeleteRow(string table, int id);

    /// <summary>One cell (null if it's empty); row is an id or column=value (a missing column=value row reads as empty).</summary>
    object? GetValue(string table, string row, string column);

    /// <summary>Sets one cell, converted to its column's type; a column=value row that doesn't exist yet is added.</summary>
    void SetValue(string table, string row, string column, object? value);

    /// <summary>Adds to a number cell (empty counts as 0) or flips a true/false one, in one step.</summary>
    void ChangeValue(string table, string row, string column, double amount);



    // locks



    /// <summary>
    /// Locks a whole table for this run until the lock is disposed: meanwhile any other run (blocks or C#) reading or
    /// changing the table waits for it. For reading, working something out and writing back without another run
    /// changing it in between. Waits for another run's lock first, at most the binding's Lock wait, then fails. Runs
    /// this one starts and waits for (<see cref="IApp.RunScriptAsync"/>) share it.
    /// <code>using (await Data.LockTableAsync("quotes")) { ... }</code>
    /// </summary>
    Task<HeldLock> LockTableAsync(string table);

    /// <summary>Locks a variable for this run until the lock is disposed, like <see cref="LockTableAsync"/>.</summary>
    Task<HeldLock> LockVariableAsync(string name);



    // cooldowns



    /// <summary>
    /// True if this label hasn't been used in the last <paramref name="seconds"/>, and starts its cooldown again;
    /// false while it's cooling down. Kept in the cooldowns table, so the same label is shared (put the user in it
    /// for one per viewer, e.g. <c>"hug " + Parameters["userLogin"]</c>).
    /// </summary>
    bool TryUseCooldown(string label, double seconds);

    /// <summary>The same, also giving how many seconds are left while it's cooling down.</summary>
    bool TryUseCooldown(string label, double seconds, out double secondsLeft);
}



/// <summary>A table row: its id and its cells by column name (an empty cell is null).</summary>
public sealed class DataRow(int id, IReadOnlyDictionary<string, object?> values)
{
    /// <summary>The row's id.</summary>
    public int Id { get; } = id;

    /// <summary>Every cell, by column name.</summary>
    public IReadOnlyDictionary<string, object?> Values { get; } = values;

    /// <summary>A cell by its column's name (null if it's empty or there's no such column).</summary>
    public object? this[string column] => Values.TryGetValue(column, out var value) ? value : null;
}
