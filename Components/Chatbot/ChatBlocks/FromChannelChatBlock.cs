using System.Collections.Generic;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Chatbot.ChatBlocks;



// lets only messages typed in some channels past this point: in a shared chat, every channel's messages reach your
// commands, this keeps some of them to your own chat (or to a friend's)
public class FromChannelChatBlock : FilterChatBlock
{
    // info



    [JsonIgnore] public override string Name => "From Channel";
    [JsonIgnore] public override string Description => "Continues into its binding or the block next to it only if the message was typed in one of these channels, otherwise the next row gets a turn. In a shared chat, the other channels' messages reach your commands too; put your own channel here to keep a command to your chat. Type login names (case and an @ in front don't matter), typing in the empty one adds the next. It doesn't match any text itself.";



    // private



    private readonly ValueSlotList _channels;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public ValueSlotList Channels => _channels;
    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("channels")]
    private List<ValueSlotData> JsonChannels
    {
        get => _channels.GetData();
        set => _channels.LoadData(value);
    }



    // constructor



    public FromChannelChatBlock()
    {
        _channels = new("Channels", typedOnly: "names are typed, chat matching can't run blocks");
        _slots = [_channels, Target];

        TrackSlots();
    }



    // matching



    protected override bool Allows(IReadOnlyDictionary<string, string> parameters) =>
        parameters.TryGetValue("fromChannelLogin", out var login) && IsUserChatBlock.IsListed(_channels, login);
}
