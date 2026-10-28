using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Model;

namespace StreamAssist.Components.Chatbot.Blocks;



// a list of patterns, each with the chat block that continues after it
// rows are tried top to bottom, the first one that matches and leads to a Bindable wins
public class MatchTextChatBlock : ChatBlock
{
    // info



    [JsonIgnore] public override string Name => "Match Text";
    [JsonIgnore] public override string Description => "Each row matches some text (or a regex) right after what's already matched, then continues into the block next to it. Spaces aren't added for you, case is ignored, named groups like (?<name>...) become parameters.";



    // private



    private readonly MatchTextRowList _rows;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public MatchTextRowList Rows => _rows;
    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("rows")]
    private List<MatchTextRowData> JsonRows
    {
        get => _rows.GetData();
        set => _rows.LoadData(value);
    }



    // constructor



    public MatchTextChatBlock()
    {
        _rows = new("");
        _slots = [_rows];

        TrackSlots();
    }



    // matching



    // flashes (like a pressed key) if any row's text matched, even if nothing after it did
    public override bool TryMatch(ChatMatchState state)
    {
        var anyRowMatched = false;

        foreach (var row in _rows.Rows.ToArray())
        {
            if (row.Pattern == "") continue;

            var match = row.Compiled.MatchAt(row.Pattern, ignoreCase: true, state.Text, state.Position, Name);
            if (match == null) continue;

            if (!anyRowMatched) RaiseTriggered();
            anyRowMatched = true;

            if (row.Child.Block is not ChatBlock child || child.IsDisabled) continue;
            if (child.TryMatch(state.Advance(match.Length, row.Compiled.NamedGroups(match)))) return true;
        }

        return false;
    }
}
