using System.Text.Json.Serialization;

namespace StreamAssist.Components.Chatbot.ChatBlocks;



// a Match Text whose rows chat can change: !addcom adds commands to it, !editcom / !delcom only change commands inside
// one (see ChatbotComponentModel.TryAddCommand). Where it sits in the tree decides what gets there first, like any block
public class EditableCommandsChatBlock : MatchTextChatBlock
{
    // info



    [JsonIgnore] public override string Name => "Editable Commands";
    [JsonIgnore] public override string Description => "A Match Text that chat's add, edit and delete command blocks (e.g. !addcom, !editcom, !delcom) can change: commands are added to the first one in the tree that fits them (one after a ! row takes !hello as hello), and only commands inside one can be edited or deleted. Rows match like a Match Text's.";
}
