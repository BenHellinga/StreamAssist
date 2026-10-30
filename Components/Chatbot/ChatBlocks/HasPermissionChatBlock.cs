using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Model;

namespace StreamAssist.Components.Chatbot.ChatBlocks;



// lets only some chatters past this point (e.g. moderators for # commands), everyone else falls through to the next row
public class HasPermissionChatBlock : ChatBlock
{
    // info



    [JsonIgnore] public override string Name => "Has Permission";
    [JsonIgnore] public override string Description => "Continues into its binding or the block next to it only if the chatter is at least this, otherwise the next row gets a turn. Each level includes the ones above it: Subscriber, then VIP, then Moderator, then Broadcaster (you). It doesn't match any text itself.";



    // constants



    // lowest to highest, with the chat parameter that says the chatter has it
    private static readonly (string Level, string Parameter)[] Levels =
    [
        ("Subscriber", "isSubscriber"),
        ("VIP", "isVip"),
        ("Moderator", "isModerator"),
        ("Broadcaster", "isBroadcaster"),
    ];



    // private



    private string _level;
    private readonly ChatTarget _target;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("At Least", ConfiguratorType.Menu)] [JsonPropertyName("level")]
    public string Level
    {
        get => _level;
        set => SetParameter(ref _level, value);
    }

    [JsonIgnore] public ChatTarget Target => _target;
    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("target")]
    private ChatTargetData JsonTarget
    {
        get => _target.GetData();
        set => _target.LoadData(value);
    }



    // constructor



    public HasPermissionChatBlock()
    {
        _level = "Moderator";
        _target = new();
        _slots = [_target];

        TrackSlots();
    }



    // public



    public static List<string> GetLevelMenuItems() => Levels.Select(l => l.Level).ToList();



    // matching



    public override bool TryMatch(ChatMatchState state)
    {
        if (!HasLevel(state.Parameters)) return false;

        RaiseTriggered(); // the chatter is allowed, flashes like a pressed key (even if nothing after it matches)
        return _target.TryMatch(state);
    }



    // helpers



    // the chosen level or any higher one (an unknown level lets nobody through)
    private bool HasLevel(IReadOnlyDictionary<string, string> parameters)
    {
        var index = Array.FindIndex(Levels, l => l.Level == _level);
        if (index < 0) return false;

        for (int i = index; i < Levels.Length; i++)
            if (parameters.TryGetValue(Levels[i].Parameter, out var value) && value == "true")
                return true;

        return false;
    }
}
