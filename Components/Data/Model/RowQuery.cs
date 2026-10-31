using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Data.Model;



// a filter for a table's rows, typed in the Data tab's query box. Terms are separated by spaces and all have to match:
//   budder / "two words"   any cell contains it (case ignored)
//   user:bud               that column contains it
//   user=budder            that column is exactly it (case ignored), user= means an empty cell
//   user!=budder           that column isn't exactly it
//   points>100 (>= < <=)   compared as numbers when both are numbers, otherwise alphabetically (dates like 2026-10-30
//                          compare right that way); an empty cell never passes a comparison
// A value with spaces goes in quotes (user="two words"). "id" works as a column too
public sealed class RowQuery
{
    // private



    private static readonly Regex TermPattern = new(
        "\\G\\s*(?:(?<column>[A-Za-z_][A-Za-z0-9_]*)(?<op>>=|<=|!=|=|:|>|<)(?:\"(?<value>[^\"]*)\"|(?<value>[^\\s\"]*))|\"(?<phrase>[^\"]*)\"|(?<word>[^\\s\"]+))\\s*");

    private readonly List<Func<TableModel, TableRow, bool>> _terms;



    // public



    public static RowQuery All { get; } = new([]);



    // constructor



    private RowQuery(List<Func<TableModel, TableRow, bool>> terms)
    {
        _terms = terms;
    }



    // public api



    // the query for this table, null (with why) if it can't be read, e.g. a column the table doesn't have
    public static RowQuery? TryParse(string text, TableModel table, out string error)
    {
        error = "";
        var terms = new List<Func<TableModel, TableRow, bool>>();
        var position = 0;
        text = text.Trim();

        while (position < text.Length)
        {
            var match = TermPattern.Match(text, position);
            if (!match.Success || match.Length == 0)
            {
                error = $"can't read '{text[position..]}'";
                return null;
            }
            position += match.Length;

            if (match.Groups["phrase"].Success || match.Groups["word"].Success)
            {
                var any = match.Groups["phrase"].Success ? match.Groups["phrase"].Value : match.Groups["word"].Value;
                terms.Add((t, row) => Cells(t, row).Any(cell => cell.Contains(any, StringComparison.OrdinalIgnoreCase)));
                continue;
            }

            var column = match.Groups["column"].Value;
            if (!string.Equals(column, "id", StringComparison.OrdinalIgnoreCase) && table.FindColumn(column) == null)
            {
                error = $"{table.Name} has no column '{column}'";
                return null;
            }

            var op = match.Groups["op"].Value;
            var value = match.Groups["value"].Value;
            terms.Add((t, row) => Matches(Cell(t, row, column), op, value));
        }

        return new RowQuery(terms);
    }



    public bool Matches(TableModel table, TableRow row) => _terms.All(term => term(table, row));



    // helpers



    private static bool Matches(string cell, string op, string value)
    {
        switch (op)
        {
            case ":": return cell.Contains(value, StringComparison.OrdinalIgnoreCase);
            case "=": return string.Equals(cell, value, StringComparison.OrdinalIgnoreCase);
            case "!=": return !string.Equals(cell, value, StringComparison.OrdinalIgnoreCase);
        }

        if (cell == "") return false;

        var order = double.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out var a)
                    && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var b)
            ? a.CompareTo(b)
            : string.Compare(cell, value, StringComparison.OrdinalIgnoreCase);

        return op switch
        {
            ">" => order > 0,
            ">=" => order >= 0,
            "<" => order < 0,
            _ => order <= 0,
        };
    }



    // a cell as text (empty cell = ""), the id too
    private static string Cell(TableModel table, TableRow row, string column) =>
        string.Equals(column, "id", StringComparison.OrdinalIgnoreCase)
            ? row.Id.ToString(CultureInfo.InvariantCulture)
            : row.Get(table.FindColumn(column)?.Name ?? column) is { } value ? ValueTypes.ToText(value) : "";



    private static IEnumerable<string> Cells(TableModel table, TableRow row) =>
        table.Columns.Select(c => Cell(table, row, c.Name)).Append(row.Id.ToString(CultureInfo.InvariantCulture));
}
