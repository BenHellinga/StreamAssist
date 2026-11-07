using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Data.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Data.Values;



// rows of a table as one text, e.g. a leaderboard: the rows a query picks (like the Data tab's), sorted by a column,
// the first Count of them, each written out with its columns as {name}s, joined by a separator
public class TableRowsBlock : ValueBlock<string>
{
    // info



    [JsonIgnore] public override string Name => "Table Rows";
    [JsonIgnore] public override string Description => "Rows of a table as text, e.g. a leaderboard: the rows Query picks (like the Data tab's query, empty = every row), sorted by Sort By (highest or lowest first, numbers as numbers), the first Count of them, each written as Row Text with its columns as {name}s and {id}, e.g. \"{user}: {total}\", joined by Separator. No rows gives empty text.";
    [JsonIgnore] public override string? Subgroup => "Read";



    // private



    private readonly MenuSlot _order;
    private readonly MenuSlot _numbers;
    private readonly ValueSlot<string> _table;
    private readonly ValueSlot<string> _query;
    private readonly ValueSlot<string> _sortBy;
    private readonly ValueSlot<int> _count;
    private readonly ValueSlot<string> _rowText;
    private readonly ValueSlot<string> _separator;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetOrderMenuItems() => ["Highest first", "Lowest first"];

    // how number cells are written in Row Text: as stored (1000090), or with thousands separators (1,000,090)
    public static List<string> GetNumbersMenuItems() => ["Plain", "1,000s"];

    // the table's name as typed (a {parameter} or a block placed in the slot works too)
    [JsonIgnore] public string Table
    {
        get => _table.Text;
        set => _table.SetText(value);
    }

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("order")]
    private ValueSlotData JsonOrder
    {
        get => _order.GetData();
        set => _order.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("numbers")]
    private ValueSlotData JsonNumbers
    {
        get => _numbers.GetData();
        set => _numbers.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("table")]
    private ValueSlotData JsonTable { get => _table.GetData(); set => _table.LoadData(value); }

    [JsonInclude] [JsonPropertyName("query")]
    private ValueSlotData JsonQuery { get => _query.GetData(); set => _query.LoadData(value); }

    [JsonInclude] [JsonPropertyName("sortBy")]
    private ValueSlotData JsonSortBy { get => _sortBy.GetData(); set => _sortBy.LoadData(value); }

    [JsonInclude] [JsonPropertyName("count")]
    private ValueSlotData JsonCount { get => _count.GetData(); set => _count.LoadData(value); }

    [JsonInclude] [JsonPropertyName("rowText")]
    private ValueSlotData JsonRowText { get => _rowText.GetData(); set => _rowText.LoadData(value); }

    [JsonInclude] [JsonPropertyName("separator")]
    private ValueSlotData JsonSeparator { get => _separator.GetData(); set => _separator.LoadData(value); }



    // constructor



    public TableRowsBlock()
    {
        _order = new("Order", GetOrderMenuItems, "Highest first");
        _numbers = new("Numbers", GetNumbersMenuItems, "Plain");
        _table = new("Table");
        _query = new("Query");
        _sortBy = new("Sort By");
        _count = new("Count");
        _rowText = new("Row Text");
        _separator = new("Separator");
        _slots = [_order, _numbers, _table, _query, _sortBy, _count, _rowText, _separator];

        _count.SetText("5");
        _separator.SetText(", ");

        TrackSlots();
    }



    // compute



    protected override async Task<string> ComputeAsync(TriggerContext context)
    {
        var order = await _order.EvaluateAsync(context);
        var data = GlobalContext.DataModel ?? throw Fail("data not set up");
        var tableName = await _table.EvaluateAsync(context);
        var table = data.FindTable(tableName) ?? throw Fail($"no table '{tableName}'");

        var queryText = await _query.EvaluateAsync(context);
        var query = RowQuery.TryParse(queryText, table, out var error) ?? throw Fail($"query '{queryText}' {error}");
        IEnumerable<TableRow> rows = table.Rows.Where(r => query.Matches(table, r));

        // sorted by a column (empty = id order), empty cells always last
        var sortName = (await _sortBy.EvaluateAsync(context)).Trim();
        if (sortName != "" && !sortName.Equals("id", StringComparison.OrdinalIgnoreCase))
        {
            var column = table.FindColumn(sortName) ?? throw Fail($"{table.Name} has no column '{sortName}'");
            var highest = order != "Lowest first";
            rows = rows.OrderBy(r => r.Get(column.Name) == null)
                .ThenBy(r => r.Get(column.Name), highest ? Comparer<object?>.Create((a, b) => CompareValues(b, a)) : Comparer<object?>.Create(CompareValues));
        }
        else if (order != "Lowest first" && sortName != "") rows = rows.OrderByDescending(r => r.Id);

        var count = await _count.EvaluateAsync(context);
        if (count < 0) throw Fail($"can't show {count} rows");

        // each row's text sees its columns (and id) as block-scoped names, like a String Builder's rows
        var numbers = await _numbers.EvaluateAsync(context);
        var texts = new List<string>();
        foreach (var row in rows.Take(count))
        {
            var scope = context.WithLocals();
            scope.SetLocal("id", row.Id.ToString());
            foreach (var column in table.Columns) scope.SetLocal(column.Name, CellText(numbers, row.Get(column.Name)));
            texts.Add(await _rowText.EvaluateAsync(scope));
        }

        return string.Join(await _separator.EvaluateAsync(context), texts);
    }



    // helpers



    private static string CellText(string numbers, object? value) => (numbers, value) switch
    {
        ("1,000s", int whole) => whole.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture),
        ("1,000s", double number) => number.ToString("#,0.##", System.Globalization.CultureInfo.InvariantCulture),
        _ => ValueTypes.ToText(value ?? ""),
    };



    // numbers as numbers, everything else as text ignoring case
    private static int CompareValues(object? a, object? b) => (a, b) switch
    {
        (int or double, int or double) => Convert.ToDouble(a).CompareTo(Convert.ToDouble(b)),
        (bool x, bool y) => x.CompareTo(y),
        _ => string.Compare(ValueTypes.ToText(a ?? ""), ValueTypes.ToText(b ?? ""), StringComparison.OrdinalIgnoreCase),
    };
}
