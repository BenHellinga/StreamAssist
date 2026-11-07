using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Chatbot.Background;



// the twitch actions blocks ask for (chat, moderation, stream, polls, rewards) and how they run: one at a time through
// the queue, as the bot or the streamer. The connection itself is in ChatbotManager.cs
public partial class ChatbotManager
{
    // public api



    // twitch actions: each waits its turn in the queue, is done as the bot if it's connected, otherwise as the streamer
    // (or not at all with "chat only as bot" ticked; channel point rewards always as the streamer), and logs why if it
    // fails (false), so the block that asked doesn't have to



    // sends a message to the streamer's channel (a repeat of the last one gets an invisible character so twitch posts it)
    // replyTo: a chat message's id to answer as a threaded reply (null or empty: a plain message)
    public Task<bool> TrySendChatMessageAsync(string message, CancellationToken ct = default, string? replyTo = null) =>
        TryRunActionAsync("send chat message", async (account, channelId, token) =>
        {
            var text = AvoidDuplicate(message, _lastSentMessage);
            await TwitchApi.SendChatMessageAsync(account, channelId, text, token, replyTo);
            _lastSentMessage = text;
        }, ct);



    // the message, or the message + DuplicateSuffix if it's the same as what was last sent, so repeats alternate
    // (yo, yo + suffix, yo, ...) and each one differs from the one before it
    public static string AvoidDuplicate(string message, string? lastSent) =>
        lastSent != null && message.Trim() == lastSent.Trim() ? message + DuplicateSuffix : message;



    // user is a login name (an @ in front is fine), the account doing it has to be a moderator
    public Task<bool> TryTimeOutUserAsync(string user, int seconds, string reason, CancellationToken ct = default) =>
        TryRunActionAsync($"time out '{user}'", async (account, channelId, token) =>
            await TwitchApi.BanUserAsync(account, channelId, await FindUserIdAsync(account, user, token), seconds, reason, token), ct);



    public Task<bool> TryBanUserAsync(string user, string reason, CancellationToken ct = default) =>
        TryRunActionAsync($"ban '{user}'", async (account, channelId, token) =>
            await TwitchApi.BanUserAsync(account, channelId, await FindUserIdAsync(account, user, token), null, reason, token), ct);



    // messageId is the chat message's id, e.g. the {messageId} parameter of a chat command
    public Task<bool> TryDeleteChatMessageAsync(string messageId, CancellationToken ct = default) =>
        TryRunActionAsync("delete chat message", (account, channelId, token) =>
            TwitchApi.DeleteChatMessageAsync(account, channelId, messageId, token), ct);



    // more moderation, as the bot if it's connected (it has to be a moderator), otherwise the streamer (see ChatOnlyAsBot)



    public Task<bool> TryUnbanUserAsync(string user, CancellationToken ct = default) =>
        TryRunActionAsync($"unban '{user}'", async (account, channelId, token) =>
            await TwitchApi.UnbanUserAsync(account, channelId, await FindUserIdAsync(account, user, token), token), ct);



    // the coloured chat banner, color from TwitchInfos.AnnouncementColors
    public Task<bool> TrySendAnnouncementAsync(string message, string color, CancellationToken ct = default) =>
        TryRunActionAsync("send announcement", (account, channelId, token) =>
            TwitchApi.SendAnnouncementAsync(account, channelId, message, color.ToLowerInvariant(), token), ct);



    // a chat mode from TwitchInfos.ChatModes on (true), off (false) or toggled (null); duration is seconds for slow
    // mode, minutes followed for followers only (null keeps twitch's current / default)
    public Task<bool> TrySetChatModeAsync(string mode, bool? on, int? duration, CancellationToken ct = default) =>
        TryRunActionAsync($"set {mode.ToLowerInvariant()}", async (account, channelId, token) =>
        {
            var (key, durationKey) = mode switch
            {
                "Slow Mode" => ("slow_mode", "slow_mode_wait_time"),
                "Followers Only" => ("follower_mode", "follower_mode_duration"),
                "Subscribers Only" => ("subscriber_mode", null),
                "Emote Only" => ("emote_mode", null),
                "Unique Chat" => ("unique_chat_mode", (string?)null),
                _ => throw new Exception($"unknown chat mode '{mode}'"),
            };

            var turnOn = on ?? !(await TwitchApi.GetChatSettingsAsync(account, channelId, token)).GetProperty(key).GetBoolean();
            var changes = new JsonObject { [key] = turnOn };
            if (turnOn && durationKey != null && duration is { } value) changes[durationKey] = value;

            await TwitchApi.UpdateChatSettingsAsync(account, channelId, changes, token);
        }, ct);



    // the channel and stream: always as the streamer



    // shown to viewers as "go follow x" (only while live, twitch limits how often)
    public Task<bool> TryShoutoutAsync(string user, CancellationToken ct = default) =>
        TryRunActionAsync($"shout out '{user}'", async (account, channelId, token) =>
            await TwitchApi.ShoutoutAsync(account, channelId, await FindUserIdAsync(account, user, token), token), ct, asStreamer: true);



    // setting is "Title" or "Category" (a category's name, the closest match if it isn't exact)
    public Task<bool> TrySetStreamInfoAsync(string setting, string value, CancellationToken ct = default) =>
        TryRunActionAsync($"set stream {setting.ToLowerInvariant()}", async (account, channelId, token) =>
        {
            var changes = setting switch
            {
                "Title" => new JsonObject { ["title"] = value },
                "Category" => new JsonObject { ["game_id"] = await TwitchApi.FindCategoryIdAsync(account, value, token) ?? throw new Exception($"no category '{value}'") },
                _ => throw new Exception($"unknown setting '{setting}'"),
            };
            await TwitchApi.UpdateChannelAsync(account, channelId, changes, token);
        }, ct, asStreamer: true);



    // a TwitchInfos.Stream entry, as its kind (string, bool or int)
    public Task<(bool Ok, object? Value)> TryGetStreamInfoAsync(TwitchInfo info, CancellationToken ct = default) =>
        TryRunActionAsync<object>($"read stream {info.Name.ToLowerInvariant()}", async (account, channelId, token) =>
        {
            switch (info.Name)
            {
                case "Title": return (await TwitchApi.GetChannelAsync(account, channelId, token)).GetProperty("title").GetString() ?? "";
                case "Category": return (await TwitchApi.GetChannelAsync(account, channelId, token)).GetProperty("game_name").GetString() ?? "";
                case "Followers": return await TwitchApi.GetFollowerCountAsync(account, channelId, token);
            }

            var stream = await TwitchApi.GetStreamAsync(account, channelId, token);
            var uptime = stream == null ? TimeSpan.Zero : DateTime.UtcNow - stream.Value.GetProperty("started_at").GetDateTime().ToUniversalTime();
            return info.Name switch
            {
                "Live" => stream != null,
                "Viewers" => stream?.GetProperty("viewer_count").GetInt32() ?? 0,
                "Uptime" => stream == null ? "" : $"{(int)uptime.TotalHours}:{uptime.Minutes:00}:{uptime.Seconds:00}",
                "Uptime Minutes" => (int)uptime.TotalMinutes,
                _ => throw new Exception($"unknown setting '{info.Name}'"),
            };
        }, ct, asStreamer: true);



    // a TwitchInfos.Follower entry for a user (login name), as its kind
    public Task<(bool Ok, object? Value)> TryGetFollowerInfoAsync(string user, TwitchInfo info, CancellationToken ct = default) =>
        TryRunActionAsync<object>($"read if '{user}' follows", async (account, channelId, token) =>
        {
            var follow = await TwitchApi.GetFollowAsync(account, channelId, await FindUserIdAsync(account, user, token), token);
            var followedAt = follow?.GetProperty("followed_at").GetDateTime().ToUniversalTime();
            return info.Name switch
            {
                "Following" => follow != null,
                "Days Following" => followedAt == null ? 0 : (int)(DateTime.UtcNow - followedAt.Value).TotalDays,
                "Followed On" => followedAt?.ToString("yyyy-MM-dd") ?? "",
                _ => throw new Exception($"unknown setting '{info.Name}'"),
            };
        }, ct, asStreamer: true);



    // a TwitchInfos.User entry for a user (login name), as its kind
    public Task<(bool Ok, object? Value)> TryGetUserInfoAsync(string user, TwitchInfo info, CancellationToken ct = default) =>
        TryRunActionAsync<object>($"read twitch user '{user}'", async (account, channelId, token) =>
        {
            var login = user.Trim().TrimStart('@').ToLowerInvariant();
            var found = await TwitchApi.GetUserInfoAsync(account, login, token) ?? throw new Exception($"there's no twitch user '{login}'");
            var created = found.GetProperty("created_at").GetDateTime().ToUniversalTime();

            // what their channel is set to: only read, it's kept even while they're offline (a shoutout's "last playing")
            if (info.Name is "Last Category" or "Last Title")
            {
                var channel = await TwitchApi.GetChannelAsync(account, found.GetProperty("id").GetString()!, token);
                return channel.GetProperty(info.Name == "Last Category" ? "game_name" : "title").GetString() ?? "";
            }

            return info.Name switch
            {
                "Display Name" => found.GetProperty("display_name").GetString() ?? "",
                "Id" => found.GetProperty("id").GetString() ?? "",
                "Account Age Days" => (int)(DateTime.UtcNow - created).TotalDays,
                "Created On" => created.ToString("yyyy-MM-dd"),
                "Description" => found.GetProperty("description").GetString() ?? "",
                _ => throw new Exception($"unknown setting '{info.Name}'"),
            };
        }, ct);



    // a clip of the last ~30 seconds (only while live), its link
    public Task<(bool Ok, string? Value)> TryCreateClipAsync(CancellationToken ct = default) =>
        TryRunActionAsync<string>("create clip", (account, channelId, token) =>
            TwitchApi.CreateClipAsync(account, channelId, token), ct, asStreamer: true);



    public Task<bool> TryCreateMarkerAsync(string description, CancellationToken ct = default) =>
        TryRunActionAsync("create stream marker", (account, channelId, token) =>
            TwitchApi.CreateMarkerAsync(account, channelId, description, token), ct, asStreamer: true);



    public Task<bool> TryRunAdAsync(int seconds, CancellationToken ct = default) =>
        TryRunActionAsync("run ad", (account, channelId, token) =>
            TwitchApi.StartCommercialAsync(account, channelId, seconds, token), ct, asStreamer: true);



    // polls, predictions, VIPs and moderators: always as the streamer (only the broadcaster can)



    public Task<bool> TryStartPollAsync(string title, IReadOnlyList<string> choices, int seconds, CancellationToken ct = default) =>
        TryRunActionAsync("start poll", (account, channelId, token) =>
            TwitchApi.StartPollAsync(account, channelId, title, choices, seconds, token), ct, asStreamer: true);



    // showResults false hides the results when it ends
    public Task<bool> TryEndPollAsync(bool showResults, CancellationToken ct = default) =>
        TryRunActionAsync("end poll", async (account, channelId, token) =>
        {
            var id = await TwitchApi.GetActivePollIdAsync(account, channelId, token) ?? throw new Exception("no poll is running");
            await TwitchApi.EndPollAsync(account, channelId, id, showResults ? "TERMINATED" : "ARCHIVED", token);
        }, ct, asStreamer: true);



    public Task<bool> TryStartPredictionAsync(string title, IReadOnlyList<string> outcomes, int seconds, CancellationToken ct = default) =>
        TryRunActionAsync("start prediction", (account, channelId, token) =>
            TwitchApi.StartPredictionAsync(account, channelId, title, outcomes, seconds, token), ct, asStreamer: true);



    // change is Lock, Resolve (winner = an outcome's title, case ignored) or Cancel
    public Task<bool> TryEndPredictionAsync(string change, string winner, CancellationToken ct = default) =>
        TryRunActionAsync($"{change.ToLowerInvariant()} prediction", async (account, channelId, token) =>
        {
            var prediction = await TwitchApi.GetCurrentPredictionAsync(account, channelId, token) ?? throw new Exception("no prediction is open");
            var id = prediction.GetProperty("id").GetString()!;

            string? winnerId = null;
            if (change == "Resolve")
            {
                var outcome = prediction.GetProperty("outcomes").EnumerateArray()
                    .FirstOrDefault(o => string.Equals(o.GetProperty("title").GetString()?.Trim(), winner.Trim(), StringComparison.OrdinalIgnoreCase));
                if (outcome.ValueKind == JsonValueKind.Undefined) throw new Exception($"no outcome '{winner}'");
                winnerId = outcome.GetProperty("id").GetString();
            }

            var status = change switch { "Lock" => "LOCKED", "Resolve" => "RESOLVED", _ => "CANCELED" };
            await TwitchApi.EndPredictionAsync(account, channelId, id, status, winnerId, token);
        }, ct, asStreamer: true);



    public Task<bool> TrySetVipAsync(string user, bool add, CancellationToken ct = default) =>
        TryRunActionAsync($"{(add ? "add" : "remove")} vip '{user}'", async (account, channelId, token) =>
            await TwitchApi.SetVipAsync(account, channelId, await FindUserIdAsync(account, user, token), add, token), ct, asStreamer: true);



    public Task<bool> TrySetModeratorAsync(string user, bool add, CancellationToken ct = default) =>
        TryRunActionAsync($"{(add ? "add" : "remove")} moderator '{user}'", async (account, channelId, token) =>
            await TwitchApi.SetModeratorAsync(account, channelId, await FindUserIdAsync(account, user, token), add, token), ct, asStreamer: true);



    // channel point rewards: always done as the streamer (only the broadcaster can manage their rewards, so the bot never
    // needs that permission). A reward is found by its id or its title (case ignored)



    // a reward's setting, as the setting's kind (int, string or bool), e.g. the Cost of "Hydrate"
    public Task<(bool Ok, object? Value)> TryGetRewardSettingAsync(string reward, RewardSetting setting, CancellationToken ct = default) =>
        TryRunActionAsync<object>($"read '{setting.Name}' of reward '{reward}'", async (account, channelId, token) =>
            setting.Read(await FindRewardAsync(account, channelId, reward, token)), ct, asStreamer: true);



    // value is the setting's kind (int, string or bool). Only works on rewards this app created
    public Task<bool> TrySetRewardSettingAsync(string reward, RewardSetting setting, object value, CancellationToken ct = default) =>
        TryRunActionAsync($"change '{setting.Name}' of reward '{reward}'", async (account, channelId, token) =>
        {
            var found = await FindRewardAsync(account, channelId, reward, token);
            var changes = new JsonObject();
            setting.Write(changes, value);

            try
            {
                await TwitchApi.UpdateCustomRewardAsync(account, channelId, found.GetProperty("id").GetString()!, changes, token);
            }
            catch (Exception ex) when (ex.Message.StartsWith("403"))
            {
                throw new Exception("it was made outside StreamAssist, and twitch only lets the app that created a reward change it (create it with New redeem in the Events tab)");
            }
        }, ct, asStreamer: true);



    public Task<bool> TryCreateRewardAsync(NewReward reward, CancellationToken ct = default) =>
        TryRunActionAsync($"create reward '{reward.Title}'", (account, channelId, token) =>
        {
            var body = new JsonObject
            {
                ["title"] = reward.Title,
                ["cost"] = reward.Cost,
                ["prompt"] = reward.Prompt,
                ["is_user_input_required"] = reward.UserInputRequired,
            };
            return TwitchApi.CreateCustomRewardAsync(account, channelId, body, token);
        }, ct, asStreamer: true);



    // actions



    // the connection is checked when it's the action's turn (it may have changed while it waited)
    private async Task<bool> TryRunActionAsync(string what, Func<TwitchSession, string, CancellationToken, Task> action, CancellationToken ct,
        bool asStreamer = false)
    {
        var result = await TryRunActionAsync<bool>(what, async (account, channelId, token) =>
        {
            await action(account, channelId, token);
            return true;
        }, ct, asStreamer);
        return result.Ok;
    }



    // the same for actions that give something back (e.g. reading a reward), Value is default when it failed
    // done as the bot if it's connected (or must be, see ChatOnlyAsBot), unless asStreamer (things only the broadcaster can do)
    private async Task<(bool Ok, T? Value)> TryRunActionAsync<T>(string what, Func<TwitchSession, string, CancellationToken, Task<T>> action,
        CancellationToken ct, bool asStreamer = false) =>
        await _actions.RunAsync<(bool, T?)>(async () =>
        {
            var streamer = _streamer;
            if (streamer == null)
            {
                AppLog.WriteError($"can't {what}, twitch not connected");
                return (false, default);
            }

            // as the bot while it's connected; with "chat only as bot" ticked never as the streamer, failing without the bot
            var bot = _bot;
            var useBot = !asStreamer && (bot != null || _model.ChatOnlyAsBot);
            if (useBot && bot == null)
            {
                AppLog.WriteError($"can't {what}, bot not connected");
                return (false, default);
            }

            var account = useBot ? TwitchAccount.Bot : TwitchAccount.Streamer;
            var session = useBot ? bot! : streamer;

            try
            {
                return (true, await action(session, streamer.UserId, ct));
            }
            catch (TwitchUnauthorizedException)
            {
                // no refresh or retry here (only startup, Connect / Retry and the background check do that), so a
                // refused login shows up: that account counts as disconnected and the action fails
                OnLoginRefused(account, session, what);
                return (false, default);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                AppLog.WriteError($"can't {what} ({ex.Message})");
                return (false, default);
            }
        }, ct);



    // lookups



    // a reward on the channel by its id or its title (case and surrounding spaces ignored)
    private static async Task<JsonElement> FindRewardAsync(TwitchSession account, string channelId, string reward, CancellationToken ct)
    {
        var name = reward.Trim();
        if (name == "") throw new Exception("no reward given");

        var rewards = await TwitchApi.GetCustomRewardsAsync(account, channelId, ct);
        foreach (var found in rewards)
            if (found.GetProperty("id").GetString() == name) return found;
        foreach (var found in rewards)
            if (string.Equals(found.GetProperty("title").GetString()?.Trim(), name, StringComparison.OrdinalIgnoreCase)) return found;

        throw new Exception($"there's no reward '{name}' on the channel");
    }



    private static async Task<string> FindUserIdAsync(TwitchSession account, string user, CancellationToken ct)
    {
        var login = user.Trim().TrimStart('@').ToLowerInvariant();
        if (login == "") throw new Exception("no user given");

        var found = await TwitchApi.GetUserAsync(account, login, ct);
        return found?.Id ?? throw new Exception($"there's no twitch user '{login}'");
    }
}
