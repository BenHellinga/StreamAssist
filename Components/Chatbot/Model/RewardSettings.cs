using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Scripting;

namespace StreamAssist.Components.Chatbot.Model;



// one setting of a channel point reward that the Get / Set Reward Setting blocks can pick: its name in the dropdown, the
// kind of value it is, how it's read from a reward (Get returns the kind's type: int, string, bool) and the change that
// sets it (Change takes the kind's type)
public sealed record RewardSetting(string Name, ValueKind Kind, string Description, System.Func<TwitchReward, object> Get, System.Func<object, RewardChanges> Change);



// every reward setting the blocks offer, in dropdown order, and how a reward is read from / written to twitch's json.
// The limits (cooldown, max per stream, max per viewer) read 0 when they're off, and writing 0 turns them off
public static class RewardSettings
{
    public static readonly IReadOnlyList<RewardSetting> All =
    [
        new("Cost", ValueKind.Int, "how many channel points it costs (at least 1)", r => r.Cost, v => new() { Cost = (int)v }),
        new("Title", ValueKind.Text, "its name (unique on the channel)", r => r.Title, v => new() { Title = (string)v }),
        new("Prompt", ValueKind.Text, "the description viewers see", r => r.Prompt, v => new() { Prompt = (string)v }),
        new("Enabled", ValueKind.Bool, "whether viewers can see it", r => r.IsEnabled, v => new() { IsEnabled = (bool)v }),
        new("Paused", ValueKind.Bool, "whether it's shown but can't be redeemed", r => r.IsPaused, v => new() { IsPaused = (bool)v }),
        new("Viewer types text", ValueKind.Bool, "whether the viewer has to type something to redeem it",
            r => r.IsUserInputRequired, v => new() { IsUserInputRequired = (bool)v }),
        new("Skip request queue", ValueKind.Bool, "whether redeems are marked done right away instead of waiting in your queue",
            r => r.SkipsRequestQueue, v => new() { SkipsRequestQueue = (bool)v }),
        new("Cooldown seconds", ValueKind.Int, "seconds before anyone can redeem it again (0 = no cooldown)",
            r => r.CooldownSeconds, v => new() { CooldownSeconds = (int)v }),
        new("Max per stream", ValueKind.Int, "how many times it can be redeemed each stream (0 = no limit)",
            r => r.MaxPerStream, v => new() { MaxPerStream = (int)v }),
        new("Max per viewer per stream", ValueKind.Int, "how many times each viewer can redeem it each stream (0 = no limit)",
            r => r.MaxPerViewerPerStream, v => new() { MaxPerViewerPerStream = (int)v }),
        new("Background color", ValueKind.Text, "its color, e.g. #9147FF", r => r.BackgroundColor, v => new() { BackgroundColor = (string)v }),
    ];



    public static IReadOnlyList<string> Names => All.Select(s => s.Name).ToList();



    public static RewardSetting? Find(string name) => All.FirstOrDefault(s => s.Name == name);



    // a reward from twitch's json
    public static TwitchReward Read(JsonElement reward) => new(
        reward.GetProperty("id").GetString() ?? "",
        reward.GetProperty("title").GetString() ?? "",
        reward.GetProperty("cost").GetInt32(),
        reward.GetProperty("prompt").GetString() ?? "",
        reward.GetProperty("is_enabled").GetBoolean(),
        reward.GetProperty("is_paused").GetBoolean(),
        reward.GetProperty("is_user_input_required").GetBoolean(),
        reward.GetProperty("should_redemptions_skip_request_queue").GetBoolean(),
        Limit(reward, "global_cooldown_setting", "global_cooldown_seconds"),
        Limit(reward, "max_per_stream_setting", "max_per_stream"),
        Limit(reward, "max_per_user_per_stream_setting", "max_per_user_per_stream"),
        reward.GetProperty("background_color").GetString() ?? "");



    // the changes as twitch's json (only what's set)
    public static JsonObject Write(RewardChanges changes)
    {
        var json = new JsonObject();
        if (changes.Title != null) json["title"] = changes.Title;
        if (changes.Cost is { } cost) json["cost"] = cost;
        if (changes.Prompt != null) json["prompt"] = changes.Prompt;
        if (changes.IsEnabled is { } enabled) json["is_enabled"] = enabled;
        if (changes.IsPaused is { } paused) json["is_paused"] = paused;
        if (changes.IsUserInputRequired is { } input) json["is_user_input_required"] = input;
        if (changes.SkipsRequestQueue is { } skip) json["should_redemptions_skip_request_queue"] = skip;
        if (changes.CooldownSeconds is { } cooldown) SetLimit(json, "is_global_cooldown_enabled", "global_cooldown_seconds", cooldown);
        if (changes.MaxPerStream is { } perStream) SetLimit(json, "is_max_per_stream_enabled", "max_per_stream", perStream);
        if (changes.MaxPerViewerPerStream is { } perViewer) SetLimit(json, "is_max_per_user_per_stream_enabled", "max_per_user_per_stream", perViewer);
        if (changes.BackgroundColor != null) json["background_color"] = changes.BackgroundColor;
        return json;
    }



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



// what the New redeem panel asks twitch to create (ChatbotComponentModel.TryRequestCreateReward)
public sealed record NewReward(string Title, int Cost, string Prompt, bool UserInputRequired);
