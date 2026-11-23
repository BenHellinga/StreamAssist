using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Background;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Core.Connections;
using StreamAssist.Scripting;
using StreamAssist.Shell;
using StreamAssist.Core.Utilities;

namespace StreamAssist.Components.CSharp.Api;



// ITwitch: checks what's asked (the ranges twitch takes), then the chatbot manager does it in its queue and logs its own
// failures (so those throw already logged)
public sealed class TwitchScriptApi(TriggerContext context) : ScriptApiPart(context), ITwitch
{
    public const int MaxTimeoutSeconds = 1_209_600; // two weeks



    private static ChatbotManager Chatbot => Need(GlobalContext.ChatbotManager, "chatbot");
    private static ChatbotComponentModel Model => Need(GlobalContext.ChatbotComponentModel, "chatbot");



    // connection



    public bool IsConnected => UiThread.Run<bool>(() => GlobalContext.ChatbotManager?.StreamerConnection == ConnectionStatus.Connected);

    public bool IsBotConnected => UiThread.Run<bool>(() => GlobalContext.ChatbotManager?.BotConnection == ConnectionStatus.Connected);



    // chat



    // an empty message sends nothing
    public Task SendMessageAsync(string message, bool reply = true, SharedChat sharedChat = SharedChat.ThisChannelOnly) => UiThread.RunAsync(async () =>
    {
        var chatbot = Chatbot;
        if (string.IsNullOrWhiteSpace(message)) return;

        var replyTo = reply && Context.Parameters.TryGetValue("messageId", out var id) ? id : null;
        if (!await chatbot.TrySendChatMessageAsync(message, Token, replyTo, sharedChat == SharedChat.ThisChannelOnly))
            throw Logged("the message wasn't sent");
    });



    public Task SendAnnouncementAsync(string message, AnnouncementColor color = AnnouncementColor.Primary) => UiThread.RunAsync(async () =>
    {
        var chatbot = Chatbot;
        if (string.IsNullOrWhiteSpace(message)) return;
        if (!await chatbot.TrySendAnnouncementAsync(message, color.ToString(), Token)) throw Logged("announcement not sent");
    });



    public Task DeleteMessageAsync(string messageId) => UiThread.RunAsync(async () =>
    {
        var chatbot = Chatbot;
        var id = messageId.Trim();
        if (id == "" || id == "{messageId}") throw Error("no message id, only chat has one");
        if (!await chatbot.TryDeleteChatMessageAsync(id, Token)) throw Logged("the message wasn't deleted");
    });



    public IReadOnlyList<ChatMessage> RecentMessages => UiThread.Run<IReadOnlyList<ChatMessage>>(() => GlobalContext.ChatbotComponentModel?.ChatHistory.Messages.Select(m => new ChatMessage(m)).ToList() ?? []);



    // moderation



    public Task TimeOutAsync(string user, int seconds, string reason = "") => UiThread.RunAsync(async () =>
    {
        var chatbot = Chatbot;
        if (seconds < 1 || seconds > MaxTimeoutSeconds) throw Error($"{seconds}s not allowed, 1 to {MaxTimeoutSeconds}");
        if (!await chatbot.TryTimeOutUserAsync(user, seconds, reason, Token)) throw Logged($"'{user}' wasn't timed out");
    });



    public Task BanAsync(string user, string reason = "") => UiThread.RunAsync(async () =>
    {
        if (!await Chatbot.TryBanUserAsync(user, reason, Token)) throw Logged($"'{user}' wasn't banned");
    });



    public Task UnbanAsync(string user) => UiThread.RunAsync(async () =>
    {
        if (!await Chatbot.TryUnbanUserAsync(user, Token)) throw Logged("not unbanned");
    });



    public Task SetChatModeAsync(ChatMode mode, Switch change, int? duration = null) => UiThread.RunAsync(async () =>
    {
        var name = mode switch
        {
            ChatMode.SlowMode => "Slow Mode",
            ChatMode.FollowersOnly => "Followers Only",
            ChatMode.SubscribersOnly => "Subscribers Only",
            ChatMode.EmoteOnly => "Emote Only",
            _ => "Unique Chat",
        };
        bool? on = change switch { Switch.On => true, Switch.Off => false, _ => null };
        if (!await Chatbot.TrySetChatModeAsync(name, on, duration, Token)) throw Logged("chat mode not set");
    });



    public Task SetVipAsync(string user, bool isVip) => UiThread.RunAsync(async () =>
    {
        if (!await Chatbot.TrySetVipAsync(user, isVip, Token)) throw Logged("vip not changed");
    });



    public Task SetModeratorAsync(string user, bool isModerator) => UiThread.RunAsync(async () =>
    {
        if (!await Chatbot.TrySetModeratorAsync(user, isModerator, Token)) throw Logged("moderator not changed");
    });



    public Task ShoutoutAsync(string user) => UiThread.RunAsync(async () =>
    {
        if (!await Chatbot.TryShoutoutAsync(user, Token)) throw Logged("no shoutout");
    });



    // channel and stream



    public Task<TwitchChannel> GetChannelAsync(string? user = null) => UiThread.RunAsync<TwitchChannel>(async () =>
    {
        var (ok, channel) = await Chatbot.TryGetChannelAsync(user, Token);
        return ok ? channel! : throw Logged("channel not read");
    });



    public Task<TwitchLiveStream?> GetLiveStreamAsync() => UiThread.RunAsync<TwitchLiveStream?>(async () =>
    {
        var (ok, stream) = await Chatbot.TryGetLiveStreamAsync(Token);
        return ok ? stream : throw Logged("stream not read");
    });



    public Task<int> GetFollowerCountAsync() => UiThread.RunAsync<int>(async () =>
    {
        var (ok, count) = await Chatbot.TryGetFollowerCountAsync(Token);
        return ok ? count : throw Logged("followers not read");
    });



    public Task<DateTime?> GetFollowedAtAsync(string user) => UiThread.RunAsync<DateTime?>(async () =>
    {
        var (ok, followedAt) = await Chatbot.TryGetFollowedAtAsync(user, Token);
        return ok ? followedAt : throw Logged("follower info not read");
    });



    public Task<TwitchUser?> GetUserAsync(string user) => UiThread.RunAsync<TwitchUser?>(async () =>
    {
        var (ok, found) = await Chatbot.TryGetUserAsync(user, Token);
        return ok ? found : throw Logged("user not read");
    });



    public Task SetTitleAsync(string title) => UiThread.RunAsync(async () =>
    {
        var chatbot = Chatbot;
        if (title.Trim() == "") throw Error("the title is empty");
        if (!await chatbot.TrySetStreamInfoAsync("Title", title.Trim(), Token)) throw Logged("stream title not set");
    });



    public Task SetCategoryAsync(string category) => UiThread.RunAsync(async () =>
    {
        var chatbot = Chatbot;
        if (category.Trim() == "") throw Error("the category is empty");
        if (!await chatbot.TrySetStreamInfoAsync("Category", category.Trim(), Token)) throw Logged("stream category not set");
    });



    public Task<string> CreateClipAsync() => UiThread.RunAsync<string>(async () =>
    {
        var (ok, url) = await Chatbot.TryCreateClipAsync(Token);
        return ok ? url! : throw Logged("no clip");
    });



    public Task<IReadOnlyList<TwitchClip>> GetClipsAsync(string channel) => UiThread.RunAsync<IReadOnlyList<TwitchClip>>(async () =>
    {
        var (ok, clips) = await Chatbot.TryGetClipsAsync(channel, Token);
        return ok ? clips! : throw Logged("clips not read");
    });



    public Task CreateMarkerAsync(string description = "") => UiThread.RunAsync(async () =>
    {
        if (!await Chatbot.TryCreateMarkerAsync(description, Token)) throw Logged("no marker");
    });



    public Task RunAdAsync(int seconds) => UiThread.RunAsync(async () =>
    {
        var chatbot = Chatbot;
        if (seconds is < 30 or > 180) throw Error($"{seconds}s not allowed, 30 to 180");
        if (!await chatbot.TryRunAdAsync(seconds, Token)) throw Logged("no ad");
    });



    // polls and predictions



    public Task StartPollAsync(string title, IEnumerable<string> choices, int seconds) => UiThread.RunAsync(async () =>
    {
        var chatbot = Chatbot;
        var trimmed = title.Trim();
        if (trimmed == "") throw Error("needs a title");
        var list = choices.Select(c => c.Trim()).Where(c => c != "").ToList();
        if (list.Count is < 2 or > 5) throw Error($"{list.Count} choices, twitch takes 2 to 5");
        if (seconds is < 15 or > 1800) throw Error($"{seconds}s not allowed, 15 to 1800");
        if (!await chatbot.TryStartPollAsync(trimmed, list, seconds, Token)) throw Logged("poll not started");
    });



    public Task EndPollAsync(bool showResults = true) => UiThread.RunAsync(async () =>
    {
        if (!await Chatbot.TryEndPollAsync(showResults, Token)) throw Logged("poll not ended");
    });



    public Task StartPredictionAsync(string title, IEnumerable<string> outcomes, int seconds) => UiThread.RunAsync(async () =>
    {
        var chatbot = Chatbot;
        var trimmed = title.Trim();
        if (trimmed == "") throw Error("needs a title");
        var list = outcomes.Select(c => c.Trim()).Where(c => c != "").ToList();
        if (list.Count is < 2 or > 10) throw Error($"{list.Count} outcomes, twitch takes 2 to 10");
        if (seconds is < 30 or > 1800) throw Error($"{seconds}s not allowed, 30 to 1800");
        if (!await chatbot.TryStartPredictionAsync(trimmed, list, seconds, Token)) throw Logged("prediction not started");
    });



    public Task LockPredictionAsync() => UiThread.RunAsync(() => EndPredictionAsync("Lock", ""));

    public Task CancelPredictionAsync() => UiThread.RunAsync(() => EndPredictionAsync("Cancel", ""));

    public Task ResolvePredictionAsync(string winner) => UiThread.RunAsync(() => winner.Trim() == "" ? throw Error("Resolve needs a Winner") : EndPredictionAsync("Resolve", winner));



    private async Task EndPredictionAsync(string change, string winner)
    {
        if (!await Chatbot.TryEndPredictionAsync(change, winner, Token)) throw Logged("prediction not ended");
    }



    // rewards



    public Task<TwitchReward> GetRewardAsync(string reward) => UiThread.RunAsync<TwitchReward>(async () =>
    {
        var (ok, found) = await Chatbot.TryGetRewardAsync(reward, Token);
        return ok ? found! : throw Logged($"reward '{reward}' wasn't read");
    });



    public Task UpdateRewardAsync(string reward, RewardChanges changes) => UiThread.RunAsync(async () =>
    {
        if (!await Chatbot.TryUpdateRewardAsync(reward, changes, Token)) throw Logged($"reward '{reward}' wasn't changed");
    });



    // chat commands: refused ones throw with the model's reason, and a short one for chat



    public void AddCommand(string command, string response) => UiThread.Run(() =>
    {
        var model = Model;
        if (!model.TryAddCommand(command, response, out var failure, logErrors: false)) throw CommandRefused(model, failure);
    });



    public void EditCommand(string command, string response) => UiThread.Run(() =>
    {
        var model = Model;
        if (!model.TryEditCommand(command, response, out var failure, logErrors: false)) throw CommandRefused(model, failure);
    });



    public void DeleteCommand(string command) => UiThread.Run(() =>
    {
        var model = Model;
        if (!model.TryDeleteCommand(command, out var failure, logErrors: false)) throw CommandRefused(model, failure);
    });



    private static ChatCommandException CommandRefused(ChatbotComponentModel model, CommandFailure failure) => new(model.CommandError, failure switch
    {
        CommandFailure.NotFound => "command not found",
        CommandFailure.AlreadyExists => "command already exists",
        CommandFailure.NotSimple => "could not edit command",
        CommandFailure.NotEditable => "command can't be edited",
        CommandFailure.NoPlace => "command can't be added",
        _ => null, // a typo in the command itself (no name, no response) gets no reply
    });
}
