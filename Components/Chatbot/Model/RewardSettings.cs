using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Chatbot.Model;



// one setting of a channel point reward that the Get / Set Reward Setting blocks can pick: its name in the dropdown, the
// kind of value it is, and how it's read from / written to twitch's reward json (Read returns the kind's type: int, string, bool)
public sealed record RewardSetting(string Name, ValueKind Kind, string Description, Func<JsonElement, object> Read, Action<JsonObject, object> Write);



// every reward setting the blocks offer, in dropdown order
// the limits (cooldown, max per stream, max per viewer) read 0 when they're off, and writing 0 turns them off
public static class RewardSettings
{
    public static readonly IReadOnlyList<RewardSetting> All =
    [
        new("Cost", ValueKind.Int, "how many channel points it costs (at least 1)",
            r => r.GetProperty("cost").GetInt32(), (o, v) => o["cost"] = (int)v),
        new("Title", ValueKind.Text, "its name (unique on the channel)",
            r => r.GetProperty("title").GetString() ?? "", (o, v) => o["title"] = (string)v),
        new("Prompt", ValueKind.Text, "the description viewers see",
            r => r.GetProperty("prompt").GetString() ?? "", (o, v) => o["prompt"] = (string)v),
        new("Enabled", ValueKind.Bool, "whether viewers can see it",
            r => r.GetProperty("is_enabled").GetBoolean(), (o, v) => o["is_enabled"] = (bool)v),
        new("Paused", ValueKind.Bool, "whether it's shown but can't be redeemed",
            r => r.GetProperty("is_paused").GetBoolean(), (o, v) => o["is_paused"] = (bool)v),
        new("Viewer types text", ValueKind.Bool, "whether the viewer has to type something to redeem it",
            r => r.GetProperty("is_user_input_required").GetBoolean(), (o, v) => o["is_user_input_required"] = (bool)v),
        new("Skip request queue", ValueKind.Bool, "whether redeems are marked done right away instead of waiting in your queue",
            r => r.GetProperty("should_redemptions_skip_request_queue").GetBoolean(), (o, v) => o["should_redemptions_skip_request_queue"] = (bool)v),
        new("Cooldown seconds", ValueKind.Int, "seconds before anyone can redeem it again (0 = no cooldown)",
            r => Limit(r, "global_cooldown_setting", "global_cooldown_seconds"),
            (o, v) => SetLimit(o, "is_global_cooldown_enabled", "global_cooldown_seconds", (int)v)),
        new("Max per stream", ValueKind.Int, "how many times it can be redeemed each stream (0 = no limit)",
            r => Limit(r, "max_per_stream_setting", "max_per_stream"),
            (o, v) => SetLimit(o, "is_max_per_stream_enabled", "max_per_stream", (int)v)),
        new("Max per viewer per stream", ValueKind.Int, "how many times each viewer can redeem it each stream (0 = no limit)",
            r => Limit(r, "max_per_user_per_stream_setting", "max_per_user_per_stream"),
            (o, v) => SetLimit(o, "is_max_per_user_per_stream_enabled", "max_per_user_per_stream", (int)v)),
        new("Background color", ValueKind.Text, "its color, e.g. #9147FF",
            r => r.GetProperty("background_color").GetString() ?? "", (o, v) => o["background_color"] = (string)v),
    ];



    public static IReadOnlyList<string> Names => All.Select(s => s.Name).ToList();



    public static RewardSetting? Find(string name) => All.FirstOrDefault(s => s.Name == name);



    // helpers



    private static int Limit(JsonElement reward, string setting, string value)
    {
        var limit = reward.GetProperty(setting);
        return limit.GetProperty("is_enabled").GetBoolean() ? limit.GetProperty(value).GetInt32() : 0;
    }



    private static void SetLimit(JsonObject changes, string enabledField, string valueField, int value)
    {
        changes[enabledField] = value > 0;
        if (value > 0) changes[valueField] = value;
    }
}



// what the New reward panel asks twitch to create (ChatbotComponentModel.TryRequestCreateReward)
public sealed record NewReward(string Title, int Cost, string Prompt, bool UserInputRequired);
