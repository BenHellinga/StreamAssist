using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Model;

namespace StreamAssist.Components.Chatbot.ChatBlocks;



// a list of chat blocks that are all tried on the same message, so more than one command can run from it (e.g. a // row
// that logs every message, and the commands after it). A Match Text's rows are different: the first that runs wins
public class TryAllChatBlock : ChatBlock
{
    // info



    [JsonIgnore] public override string Name => "Try All";
    [JsonIgnore] public override string Description => "Tries every chat block inside on the message, top to bottom, from where it's got to: each one that reaches a binding runs it, so several can run from one message. Inside a Match Text only the first row that runs counts, put a Try All after a row to run more than one.";



    // private



    private readonly BlockSlotList _children;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public BlockSlotList Children => _children;
    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("children")]
    private List<PendingBlockData> JsonChildren
    {
        get => _children.GetData();
        set => _children.LoadData(value);
    }



    // constructor



    public TryAllChatBlock()
    {
        _children = new("", typeof(ChatBlock));
        _slots = [_children];

        TrackSlots();
    }



    // matching



    // every child gets the same state (none of them uses up text for the others), true if any of them ran a binding
    public override bool TryMatch(ChatMatchState state)
    {
        var ran = false;
        foreach (var child in _children.Blocks.OfType<ChatBlock>().Where(c => !c.IsDisabled).ToArray())
            ran |= child.TryMatch(state);

        if (ran) RaiseTriggered();
        return ran;
    }
}
