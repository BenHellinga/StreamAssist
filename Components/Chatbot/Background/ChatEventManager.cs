using System;
using System.Collections.Generic;
using StreamAssist.Components.Chatbot.Model;

namespace StreamAssist.Components.Chatbot.Background;



// runs the redeem and stream event (sub, raid, ...) bindings, with the event's details as parameters
// the parameter names are listed in ChatEvents
public class ChatEventManager : IDisposable
{
    // private



    private readonly ChatbotComponentModel _model;
    private readonly ChatbotManager _manager;



    // constructor



    public ChatEventManager(ChatbotComponentModel model, ChatbotManager manager)
    {
        _model = model;
        _manager = manager;

        _manager.EventReceived += OnEventReceived;
    }



    // background events



    private void OnEventReceived(TwitchEvent e) => Dispatch(_model, e);



    // public api



    // runs whatever binding this event is for (anything that isn't a redeem or stream event is ignored)
    public static void Dispatch(ChatbotComponentModel model, TwitchEvent e)
    {
        switch (e.Type)
        {
            case TwitchEvent.Redemption:
                var redeem = Parameters(e, "user");
                redeem["reward"] = e.GetString("reward", "title") ?? "";
                redeem["rewardId"] = e.GetString("reward", "id") ?? "";
                redeem["cost"] = e.GetString("reward", "cost") ?? "";
                redeem["input"] = e.GetString("user_input") ?? "";
                model.Redeems.TryRun(redeem["reward"], redeem);
                break;

            case TwitchEvent.Follow:
                model.TryRunEvent(ChatEvents.Follow, Parameters(e, "user"));
                break;

            // a new sub (gifted ones are left to Gift Subs, which gets one event for the whole gift)
            case TwitchEvent.Subscribe:
                if (IsTrue(e.GetString("is_gift"))) break;

                var sub = Parameters(e, "user");
                sub["tier"] = Tier(e.GetString("tier"));
                sub["months"] = "1";
                sub["streak"] = "1";
                sub["message"] = "";
                model.TryRunEvent(ChatEvents.Sub, sub);
                break;

            // a resub, only sent when the viewer shares their resub message in chat
            case TwitchEvent.Resubscribe:
                var resub = Parameters(e, "user");
                resub["tier"] = Tier(e.GetString("tier"));
                resub["months"] = e.GetString("cumulative_months") ?? "";
                resub["streak"] = e.GetString("streak_months") ?? ""; // empty if they hid it
                resub["message"] = e.GetString("message", "text") ?? "";
                model.TryRunEvent(ChatEvents.Sub, resub);
                break;

            case TwitchEvent.SubscriptionGift:
                var anonymousGift = IsTrue(e.GetString("is_anonymous"));
                var gift = Parameters(e, "user", anonymousGift);
                gift["count"] = e.GetString("total") ?? "";
                gift["tier"] = Tier(e.GetString("tier"));
                gift["anonymous"] = anonymousGift ? "true" : "false";
                gift["totalGifted"] = e.GetString("cumulative_total") ?? ""; // empty if anonymous or hidden
                model.TryRunEvent(ChatEvents.GiftSubs, gift);
                break;

            case TwitchEvent.Cheer:
                var anonymousCheer = IsTrue(e.GetString("is_anonymous"));
                var cheer = Parameters(e, "user", anonymousCheer);
                cheer["bits"] = e.GetString("bits") ?? "";
                cheer["message"] = e.GetString("message") ?? "";
                cheer["anonymous"] = anonymousCheer ? "true" : "false";
                model.TryRunEvent(ChatEvents.Cheer, cheer);
                break;

            case TwitchEvent.Raid:
                var raid = Parameters(e, "from_broadcaster_user");
                raid["viewers"] = e.GetString("viewers") ?? "";
                model.TryRunEvent(ChatEvents.Raid, raid);
                break;

            case TwitchEvent.StreamOnline:
                model.TryRunEvent(ChatEvents.StreamOnline, Parameters(e, null));
                break;

            case TwitchEvent.StreamOffline:
                model.TryRunEvent(ChatEvents.StreamOffline, Parameters(e, null));
                break;
        }
    }



    // helpers



    // the parameters every event gets, plus user/userLogin/userId from e.g. "user" -> user_name, user_login, user_id
    private static Dictionary<string, string> Parameters(TwitchEvent e, string? userPrefix, bool anonymous = false)
    {
        var parameters = new Dictionary<string, string>
        {
            ["channel"] = e.GetString("broadcaster_user_name") ?? e.GetString("to_broadcaster_user_name") ?? "",
            ["channelLogin"] = e.GetString("broadcaster_user_login") ?? e.GetString("to_broadcaster_user_login") ?? "",
            ["channelId"] = e.GetString("broadcaster_user_id") ?? e.GetString("to_broadcaster_user_id") ?? "",
            ["timestamp"] = e.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
        };

        if (userPrefix == null) return parameters;

        parameters["user"] = anonymous ? "Anonymous" : e.GetString($"{userPrefix}_name") ?? "";
        parameters["userLogin"] = anonymous ? "" : e.GetString($"{userPrefix}_login") ?? "";
        parameters["userId"] = anonymous ? "" : e.GetString($"{userPrefix}_id") ?? "";
        return parameters;
    }



    // twitch says 1000/2000/3000
    private static string Tier(string? tier) => tier switch
    {
        "1000" => "1",
        "2000" => "2",
        "3000" => "3",
        _ => tier ?? "",
    };



    private static bool IsTrue(string? value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);



    // disposal



    public void Dispose() => _manager.EventReceived -= OnEventReceived;
}
