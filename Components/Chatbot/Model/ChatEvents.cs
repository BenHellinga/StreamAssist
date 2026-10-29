using System;
using System.Collections.Generic;
using System.Linq;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Chatbot.Model;



// the stream events (not chat messages or redeems) that each get one binding, and the parameters every chatbot
// trigger gets (shown in the editor's Parameters list). ChatEventManager and TwitchEvent build the real parameters
// from twitch, the Sample... methods build the same ones for the test panels (the engine tests check they all match)
public static class ChatEvents
{
    public const string Follow = "follow";
    public const string Sub = "sub"; // new subs and shared resubs (gifted subs are left to Gift Subs)
    public const string GiftSubs = "giftSubs";
    public const string Cheer = "cheer";
    public const string Raid = "raid";
    public const string StreamOnline = "streamOnline";
    public const string StreamOffline = "streamOffline";



    // in the order they're shown
    public static readonly IReadOnlyList<(string Key, string Name)> All =
    [
        (Follow, "Follow"),
        (Sub, "Sub"),
        (GiftSubs, "Gift Subs"),
        (Cheer, "Cheer"),
        (Raid, "Raid"),
        (StreamOnline, "Stream Online"),
        (StreamOffline, "Stream Offline"),
    ];



    // parameters



    // every chatbot trigger gets these
    public static readonly IReadOnlyList<ParameterInfo> Common =
    [
        new("channel", ValueKind.Text, "your channel's display name"),
        new("channelLogin", ValueKind.Text, "your channel's login name (lowercase)"),
        new("channelId", ValueKind.Text, "your channel's twitch id"),
        new("timestamp", ValueKind.Text, "when it happened, e.g. 2026-10-28 18:30:00"),
    ];

    private static IReadOnlyList<ParameterInfo> User(string who) =>
    [
        new("user", ValueKind.Text, $"{who}'s display name"),
        new("userLogin", ValueKind.Text, $"{who}'s login name (lowercase)"),
        new("userId", ValueKind.Text, $"{who}'s twitch id"),
    ];

    // a chat message (chat commands add rest, and whatever their Parameter blocks and regex groups capture)
    public static readonly IReadOnlyList<ParameterInfo> Chat =
    [
        .. User("the chatter"),
        new("message", ValueKind.Text, "the whole message"),
        new("messageId", ValueKind.Text, "the message's twitch id"),
        new("color", ValueKind.Text, "the chatter's name color, e.g. #00FF7F (empty if they never picked one)"),
        new("badges", ValueKind.Text, "the chatter's badges, e.g. moderator,subscriber"),
        new("isBroadcaster", ValueKind.Bool, "true if it's you"),
        new("isModerator", ValueKind.Bool, "true if the chatter is a moderator"),
        new("isVip", ValueKind.Bool, "true if the chatter is a VIP"),
        new("isSubscriber", ValueKind.Bool, "true if the chatter is subscribed"),
        .. Common,
    ];

    public static readonly ParameterInfo Rest = new("rest", ValueKind.Text, "the rest of the message after what the command matched");

    public static readonly IReadOnlyList<ParameterInfo> Redeem =
    [
        .. User("the viewer who redeemed it"),
        new("reward", ValueKind.Text, "the reward's title"),
        new("rewardId", ValueKind.Text, "the reward's twitch id"),
        new("cost", ValueKind.Int, "how many channel points it cost"),
        new("input", ValueKind.Text, "what the viewer typed (if the reward asks for text)"),
        .. Common,
    ];



    // the parameters a stream event's binding gets
    public static IReadOnlyList<ParameterInfo> Parameters(string key) => key switch
    {
        Follow => [.. User("the follower"), .. Common],
        Sub =>
        [
            .. User("the subscriber"),
            new("tier", ValueKind.Int, "1, 2 or 3 (Prime counts as 1)"),
            new("months", ValueKind.Int, "how many months in total, 1 for a new sub"),
            new("streak", ValueKind.Int, "months in a row (empty if they hid it)"),
            new("message", ValueKind.Text, "their resub message (empty for a new sub)"),
            .. Common,
        ],
        GiftSubs =>
        [
            .. User("the gifter (\"Anonymous\" if hidden)"),
            new("count", ValueKind.Int, "how many subs they gifted this time"),
            new("tier", ValueKind.Int, "1, 2 or 3"),
            new("anonymous", ValueKind.Bool, "true if the gifter is hidden"),
            new("totalGifted", ValueKind.Int, "how many they've gifted in total (empty if hidden)"),
            .. Common,
        ],
        Cheer =>
        [
            .. User("the cheerer (\"Anonymous\" if hidden)"),
            new("bits", ValueKind.Int, "how many bits"),
            new("message", ValueKind.Text, "the cheer message"),
            new("anonymous", ValueKind.Bool, "true if the cheerer is hidden"),
            .. Common,
        ],
        Raid => [.. User("the raider"), new("viewers", ValueKind.Int, "how many viewers came with the raid"), .. Common],
        _ => Common,
    };



    // made-up parameters for testing a binding without twitch: amount is used as months/count/bits/viewers/cost, text as message/input
    public static Dictionary<string, string> SampleParameters(string? key, string user, string amount, string tier, string text, string? reward = null)
    {
        var login = user.ToLowerInvariant().Replace(" ", "");
        var parameters = new Dictionary<string, string>
        {
            ["channel"] = "TestChannel",
            ["channelLogin"] = "testchannel",
            ["channelId"] = "0",
            ["timestamp"] = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ["user"] = user,
            ["userLogin"] = login,
            ["userId"] = "0",
        };

        switch (key)
        {
            case Sub:
                parameters["tier"] = tier;
                parameters["months"] = amount;
                parameters["streak"] = amount;
                parameters["message"] = text;
                break;

            case GiftSubs:
                parameters["count"] = amount;
                parameters["tier"] = tier;
                parameters["anonymous"] = "false";
                parameters["totalGifted"] = amount;
                break;

            case Cheer:
                parameters["bits"] = amount;
                parameters["message"] = text;
                parameters["anonymous"] = "false";
                break;

            case Raid:
                parameters["viewers"] = amount;
                break;

            case null: // a redeem
                parameters["reward"] = reward ?? "";
                parameters["rewardId"] = "0";
                parameters["cost"] = amount;
                parameters["input"] = text;
                break;
        }

        // only what the real event has (e.g. Stream Online has no user)
        var listed = (key == null ? Redeem : Parameters(key)).Select(p => p.Name).ToHashSet();
        foreach (var name in parameters.Keys.Where(name => !listed.Contains(name)).ToList())
            parameters.Remove(name);

        return parameters;
    }



    // a made-up chat message's parameters for the chat command test panel (the same names TwitchEvent.ChatParameters gives)
    public static Dictionary<string, string> SampleChatParameters(string user, string message, bool isModerator, bool isVip, bool isSubscriber)
    {
        var badges = new List<string>();
        if (isModerator) badges.Add("moderator");
        if (isVip) badges.Add("vip");
        if (isSubscriber) badges.Add("subscriber");

        var parameters = SampleParameters(Follow, user, "", "", "");
        parameters["message"] = message;
        parameters["messageId"] = "0";
        parameters["color"] = "";
        parameters["badges"] = string.Join(",", badges);
        parameters["isBroadcaster"] = "false";
        parameters["isModerator"] = isModerator ? "true" : "false";
        parameters["isVip"] = isVip ? "true" : "false";
        parameters["isSubscriber"] = isSubscriber ? "true" : "false";
        return parameters;
    }
}



// one stream event's binding slot (e.g. Raid)
public sealed class ChatEventSlot : IDisposable
{
    // private



    private readonly string _key;
    private readonly string _name;
    private readonly ChatTarget _target;



    // public



    public string Key => _key;
    public string Name => _name;
    public ChatTarget Target => _target; // binding only, no chat blocks



    // constructor



    public ChatEventSlot(string key, string name)
    {
        _key = key;
        _name = name;
        _target = new(allowsBlocks: false);
    }



    // disposal



    public void Dispose() => _target.Dispose();
}
