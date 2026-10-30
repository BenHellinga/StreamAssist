using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Model;

namespace StreamAssist.Components.Chatbot.ChatBlocks;



// a list of patterns, each with the binding it runs or the chat block that continues after it
// rows are tried top to bottom, the first one that matches and leads to a binding wins
public class MatchTextChatBlock : ChatBlock
{
    // info



    [JsonIgnore] public override string Name => "Match Text";
    [JsonIgnore] public override string Description => "Each row matches some text right after what's already matched, then runs its binding or continues into the block next to it. Plain text matches whole words, case ignored (lurk isn't lurker), and spaces only matter between words (! then lurk is !lurk, pay then all is pay all). A /regex/ in slashes is matched exactly as written, its named groups like (?<name>...) become parameters, and // matches nothing, so always (e.g. a last row for anything else).";



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
            if (!row.TryMatchAt(state, Name, out var length, out var groups)) continue;

            if (!anyRowMatched) RaiseTriggered();
            anyRowMatched = true;

            if (row.Target.TryMatch(state.Advance(length, groups))) return true;
        }

        return false;
    }
}
