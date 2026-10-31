using System.Collections.Generic;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Chatbot.Model;



// where matching a message has got to: the text, how much of it is matched so far, and the parameters collected
public sealed record ChatMatchState(string Text, int Position, IReadOnlyDictionary<string, string> Parameters)
{
    // the state after matching `length` more characters, with extra parameters added (each branch gets its own copy)
    public ChatMatchState Advance(int length, IEnumerable<KeyValuePair<string, string>> newParameters)
    {
        var parameters = new Dictionary<string, string>(Parameters);
        foreach (var (name, value) in newParameters)
            parameters[name] = value;

        return this with { Position = Position + length, Parameters = parameters };
    }
}



// base for the blocks in the chatbot's command tree (match text, parameter)
// they reuse the block system for slots, saving and the editor, but only go in chat block slots
public abstract class ChatBlock : BlockModel
{
    [JsonIgnore] protected override BlockGroup KindGroup => BlockGroup.Chat;



    // tries to match the message from state.Position, returns true once a binding has been run (the search stops)
    public abstract bool TryMatch(ChatMatchState state);



    // starts matching at the block in a slot (e.g. the chatbot's root), an empty or disabled slot matches nothing
    public static bool TryMatchSlot(BlockSlot slot, ChatMatchState state) =>
        slot.Block is ChatBlock chatBlock && !chatBlock.IsDisabled && chatBlock.TryMatch(state);
}
