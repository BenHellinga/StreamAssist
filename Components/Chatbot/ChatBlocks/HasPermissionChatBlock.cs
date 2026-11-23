using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Model;

namespace StreamAssist.Components.Chatbot.ChatBlocks;



// lets only some chatters past this point (e.g. moderators for # commands), everyone else falls through to the next row
public class HasPermissionChatBlock : FilterChatBlock
{
    // info



    [JsonIgnore] public override string Name => "Has Permission";
    [JsonIgnore] public override string Description => "Continues into its binding or the block next to it only if the chatter is at least this, otherwise the next row gets a turn. Each level includes the ones above it: Anyone, then Subscriber, VIP, Moderator, Broadcaster (you). It doesn't match any text itself.";



    // constants



    // lowest to highest, with the chat parameter that says the chatter has it (Anyone: everyone has it)
    private static readonly (string Level, string? Parameter)[] Levels =
    [
        ("Anyone", null),
        ("Subscriber", "isSubscriber"),
        ("VIP", "isVip"),
        ("Moderator", "isModerator"),
        ("Broadcaster", "isBroadcaster"),
    ];



    // private



    private string _level;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("At Least", ConfiguratorType.Menu)] [JsonPropertyName("level")]
    public string Level
    {
        get => _level;
        set => SetParameter(ref _level, value);
    }

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // constructor



    public HasPermissionChatBlock()
    {
        _level = "Moderator";
        _slots = [Target];

        TrackSlots();
    }



    // public



    public static List<string> GetLevelMenuItems() => Levels.Select(l => l.Level).ToList();



    // matching



    // the chosen level or any higher one (an unknown level lets nobody through)
    protected override bool Allows(IReadOnlyDictionary<string, string> parameters)
    {
        var index = Array.FindIndex(Levels, l => l.Level == _level);
        if (index < 0) return false;

        for (int i = index; i < Levels.Length; i++)
            if (Levels[i].Parameter is not { } parameter || parameters.TryGetValue(parameter, out var value) && value == "true")
                return true;

        return false;
    }
}
