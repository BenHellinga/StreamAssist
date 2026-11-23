using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Chatbot.ChatBlocks;



// lets only some chatters past this point, by name (e.g. a command just for a friend)
public class IsUserChatBlock : FilterChatBlock
{
    // info



    [JsonIgnore] public override string Name => "Is User";
    [JsonIgnore] public override string Description => "Continues into its binding or the block next to it only if the chatter is one of these users, otherwise the next row gets a turn. Type their login names (case and an @ in front don't matter), typing in the empty one adds the next. It doesn't match any text itself.";



    // private



    private readonly ValueSlotList _users;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public ValueSlotList Users => _users;
    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("users")]
    private List<ValueSlotData> JsonUsers
    {
        get => _users.GetData();
        set => _users.LoadData(value);
    }



    // constructor



    public IsUserChatBlock()
    {
        _users = new("Users", typedOnly: "names are typed, chat matching can't run blocks");
        _slots = [_users, Target];

        TrackSlots();
    }



    // matching



    protected override bool Allows(IReadOnlyDictionary<string, string> parameters) =>
        parameters.TryGetValue("userLogin", out var login) && IsListed(_users, login);



    // whether a login name is in a typed list of names (an @ in front and case ignored)
    public static bool IsListed(ValueSlotList names, string login) =>
        names.FilledTexts().Any(name => string.Equals(name.TrimStart('@'), login, StringComparison.OrdinalIgnoreCase));
}
