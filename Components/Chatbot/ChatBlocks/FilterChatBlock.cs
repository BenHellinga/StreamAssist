using System.Collections.Generic;
using System.Text.Json.Serialization;
using StreamAssist.Components.Chatbot.Model;

namespace StreamAssist.Components.Chatbot.ChatBlocks;



// a chat block that matches no text, it only lets some messages past (by who sent it, where it was typed, ...) into its
// binding or the block next to it; everything else falls through to the next row
public abstract class FilterChatBlock : ChatBlock
{
    // private



    private readonly ChatTarget _target;



    // public



    [JsonIgnore] public ChatTarget Target => _target;



    // json



    [JsonInclude] [JsonPropertyName("target")]
    protected ChatTargetData JsonTarget
    {
        get => _target.GetData();
        set => _target.LoadData(value);
    }



    // constructor



    // the owner lists Target in its Slots (with any of its own) and calls TrackSlots
    protected FilterChatBlock()
    {
        _target = new();
    }



    // whether this message gets past, from the chat parameters (user, badges, fromChannel, ...)
    protected abstract bool Allows(IReadOnlyDictionary<string, string> parameters);



    // matching



    public override bool TryMatch(ChatMatchState state)
    {
        if (!Allows(state.Parameters)) return false;

        RaiseTriggered(); // let past, flashes like a pressed key (even if nothing after it matches)
        return _target.TryMatch(state);
    }
}
