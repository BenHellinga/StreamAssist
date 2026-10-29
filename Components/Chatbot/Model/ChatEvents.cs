using System;
using System.Collections.Generic;

namespace StreamAssist.Components.Chatbot.Model;



// the stream events (not chat messages or redeems) that each get one binding, and the parameters each one gets
// every event also gets channel, channelLogin, channelId and timestamp
// ChatEventManager builds the real parameters from twitch, SampleParameters builds the same ones for the test buttons
public static class ChatEvents
{
    public const string Follow = "follow";               // user, userLogin, userId
    public const string Sub = "sub";                     // user, userLogin, userId, tier (1-3), months (1 = new sub), streak, message (gifted subs are left to Gift Subs)
    public const string GiftSubs = "giftSubs";           // user ("Anonymous" if hidden), userLogin, userId, count, tier, anonymous, totalGifted
    public const string Cheer = "cheer";                 // user ("Anonymous" if hidden), userLogin, userId, bits, message, anonymous
    public const string Raid = "raid";                   // user (the raider), userLogin, userId, viewers
    public const string StreamOnline = "streamOnline";   // (only the common ones)
    public const string StreamOffline = "streamOffline"; // (only the common ones)

    // a redeem gets user, userLogin, userId, reward, rewardId, cost, input (what the viewer typed, if the reward asks)



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
