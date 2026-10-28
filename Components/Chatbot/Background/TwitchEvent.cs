using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace StreamAssist.Components.Chatbot.Background;



// one event from twitch (chat message, redeem, sub, ...), Type is the eventsub name and Data its payload
// field names are listed at https://dev.twitch.tv/docs/eventsub/eventsub-subscription-types
public sealed class TwitchEvent(string type, JsonElement data, DateTimeOffset timestamp)
{
    // types the chatbot subscribes to
    public const string ChatMessage = "channel.chat.message";
    public const string ChatNotification = "channel.chat.notification";
    public const string Redemption = "channel.channel_points_custom_reward_redemption.add";
    public const string Subscribe = "channel.subscribe";
    public const string SubscriptionGift = "channel.subscription.gift";
    public const string Resubscribe = "channel.subscription.message";
    public const string Cheer = "channel.cheer";
    public const string Raid = "channel.raid";
    public const string Follow = "channel.follow";
    public const string StreamOnline = "stream.online";
    public const string StreamOffline = "stream.offline";



    public string Type { get; } = type;
    public JsonElement Data { get; } = data;
    public DateTimeOffset Timestamp { get; } = timestamp; // when twitch sent it



    // reads a (nested) text field, e.g. GetString("message", "text"), null if it's missing
    public string? GetString(params string[] path)
    {
        var current = Data;

        foreach (var name in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(name, out current)) return null;
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => current.ToString(),
        };
    }



    // the parameters every chat trigger gets, before anything the chat blocks add
    public Dictionary<string, string> ChatParameters()
    {
        var badges = Data.TryGetProperty("badges", out var list) && list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Select(b => b.TryGetProperty("set_id", out var id) ? id.GetString() ?? "" : "").Where(b => b != "").ToList()
            : [];

        return new()
        {
            ["user"] = GetString("chatter_user_name") ?? "",
            ["userLogin"] = GetString("chatter_user_login") ?? "",
            ["userId"] = GetString("chatter_user_id") ?? "",
            ["message"] = GetString("message", "text") ?? "",
            ["messageId"] = GetString("message_id") ?? "",
            ["channel"] = GetString("broadcaster_user_name") ?? "",
            ["channelLogin"] = GetString("broadcaster_user_login") ?? "",
            ["channelId"] = GetString("broadcaster_user_id") ?? "",
            ["timestamp"] = Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
            ["color"] = GetString("color") ?? "",
            ["badges"] = string.Join(",", badges),
            ["isBroadcaster"] = badges.Contains("broadcaster") ? "true" : "false",
            ["isModerator"] = badges.Contains("moderator") ? "true" : "false",
            ["isVip"] = badges.Contains("vip") ? "true" : "false",
            ["isSubscriber"] = badges.Contains("subscriber") || badges.Contains("founder") ? "true" : "false",
        };
    }
}
