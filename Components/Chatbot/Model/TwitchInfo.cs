using System.Collections.Generic;
using System.Linq;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Chatbot.Model;



// one thing the Get Stream Info / Get Follower Info / Get Twitch User blocks can read: its dropdown name and the kind of
// value it is (picking it picks the block's type, like Get Reward Setting)
public sealed record TwitchInfo(string Name, ValueKind Kind, string Description);



// what each of those blocks offers, in dropdown order (ChatbotManager reads them), plus Set Chat Mode's modes
public static class TwitchInfos
{
    public static readonly IReadOnlyList<TwitchInfo> Stream =
    [
        new("Title", ValueKind.Text, "the stream title"),
        new("Category", ValueKind.Text, "the category (game) name"),
        new("Live", ValueKind.Bool, "whether you're live right now"),
        new("Viewers", ValueKind.Int, "viewers right now (0 while offline)"),
        new("Uptime Minutes", ValueKind.Int, "minutes since the stream started (0 while offline)"),
        new("Followers", ValueKind.Int, "how many followers the channel has"),
    ];

    public static readonly IReadOnlyList<TwitchInfo> Follower =
    [
        new("Following", ValueKind.Bool, "whether they follow the channel"),
        new("Days Following", ValueKind.Int, "whole days since they followed (0 if they don't)"),
        new("Followed On", ValueKind.Text, "the date they followed, e.g. 2024-03-17 (empty if they don't)"),
    ];

    public static readonly IReadOnlyList<TwitchInfo> User =
    [
        new("Display Name", ValueKind.Text, "their name as shown in chat"),
        new("Id", ValueKind.Text, "their twitch id"),
        new("Account Age Days", ValueKind.Int, "whole days since the account was made"),
        new("Created On", ValueKind.Text, "the date the account was made, e.g. 2019-06-02"),
        new("Description", ValueKind.Text, "their channel's bio"),
    ];

    // Set Chat Mode: slow mode takes seconds (3 to 120), followers only takes minutes followed (0 to 129600)
    public static readonly IReadOnlyList<string> ChatModes = ["Slow Mode", "Followers Only", "Subscribers Only", "Emote Only", "Unique Chat"];

    public static readonly IReadOnlyList<string> AnnouncementColors = ["Primary", "Blue", "Green", "Orange", "Purple"];



    public static TwitchInfo? Find(IReadOnlyList<TwitchInfo> list, string name) => list.FirstOrDefault(i => i.Name == name);



    public static List<string> Names(IReadOnlyList<TwitchInfo> list) => list.Select(i => i.Name).ToList();
}
