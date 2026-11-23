using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StreamAssist.Scripting;



/// <summary>
/// Your Twitch channel. Requests wait their turn (one per second, shared with the blocks), are done as the bot if it's
/// connected and otherwise as you (unless Chat only as bot is ticked), except the ones only the broadcaster can do,
/// which are always done as you. Users are login names (an @ in front is fine). A request that fails is logged and
/// throws a <see cref="StreamAssistException"/>.
/// </summary>
public interface ITwitch
{
    // connection



    /// <summary>Whether your (the streamer's) account is connected (the Connections tab).</summary>
    bool IsConnected { get; }

    /// <summary>Whether the bot account is connected.</summary>
    bool IsBotConnected { get; }



    // chat



    /// <summary>
    /// Sends a chat message. <paramref name="reply"/> answers the chat message that started this run as a threaded
    /// reply (a plain message when no chat message started it). During a shared chat, <paramref name="sharedChat"/>
    /// keeps it in your chat or sends it to every channel's.
    /// </summary>
    Task SendMessageAsync(string message, bool reply = true, SharedChat sharedChat = SharedChat.ThisChannelOnly);

    /// <summary>Sends a highlighted announcement (the bot or you has to be a moderator).</summary>
    Task SendAnnouncementAsync(string message, AnnouncementColor color = AnnouncementColor.Primary);

    /// <summary>Deletes a chat message by its id, e.g. <c>Parameters["messageId"]</c> (the bot or you has to be a moderator).</summary>
    Task DeleteMessageAsync(string messageId);

    /// <summary>The last 100 chat messages since the app started, oldest first.</summary>
    IReadOnlyList<ChatMessage> RecentMessages { get; }



    // moderation



    /// <summary>Times a user out for 1 to 1209600 seconds (two weeks).</summary>
    Task TimeOutAsync(string user, int seconds, string reason = "");

    /// <summary>Bans a user.</summary>
    Task BanAsync(string user, string reason = "");

    /// <summary>Unbans a user, or ends their timeout.</summary>
    Task UnbanAsync(string user);

    /// <summary>
    /// Turns a chat mode on or off. <paramref name="duration"/> is seconds between messages for slow mode (3 to 120),
    /// minutes followed for followers only; null keeps Twitch's current one.
    /// </summary>
    Task SetChatModeAsync(ChatMode mode, Switch change, int? duration = null);

    /// <summary>Makes a user a VIP, or removes it (done as you).</summary>
    Task SetVipAsync(string user, bool isVip);

    /// <summary>Makes a user a moderator, or removes it (done as you).</summary>
    Task SetModeratorAsync(string user, bool isModerator);

    /// <summary>Gives a channel a shoutout (only while live, Twitch limits how often; done as you).</summary>
    Task ShoutoutAsync(string user);



    // channel and stream (done as you)



    /// <summary>A channel's title and category: yours, or another user's (what they last streamed).</summary>
    Task<TwitchChannel> GetChannelAsync(string? user = null);

    /// <summary>Your stream while you're live, null while you're offline.</summary>
    Task<TwitchLiveStream?> GetLiveStreamAsync();

    /// <summary>How many followers your channel has.</summary>
    Task<int> GetFollowerCountAsync();

    /// <summary>When a user followed you, null if they don't.</summary>
    Task<DateTime?> GetFollowedAtAsync(string user);

    /// <summary>A Twitch account, null if there's none with that login.</summary>
    Task<TwitchUser?> GetUserAsync(string user);

    /// <summary>Changes your stream title.</summary>
    Task SetTitleAsync(string title);

    /// <summary>Changes your category by its name (the closest match if it isn't exact), e.g. "Just Chatting".</summary>
    Task SetCategoryAsync(string category);

    /// <summary>Clips the last ~30 seconds (only while live) and gives its link.</summary>
    Task<string> CreateClipAsync();

    /// <summary>A channel's most viewed clips (up to 100).</summary>
    Task<IReadOnlyList<TwitchClip>> GetClipsAsync(string channel);

    /// <summary>Marks this moment of the stream for the VOD (only while live).</summary>
    Task CreateMarkerAsync(string description = "");

    /// <summary>Starts an ad break of 30 to 180 seconds (affiliates and partners only).</summary>
    Task RunAdAsync(int seconds);



    // polls and predictions (done as you)



    /// <summary>Starts a poll with 2 to 5 choices, running 15 to 1800 seconds.</summary>
    Task StartPollAsync(string title, IEnumerable<string> choices, int seconds);

    /// <summary>Ends the running poll, showing its results or hiding them.</summary>
    Task EndPollAsync(bool showResults = true);

    /// <summary>Starts a prediction with 2 to 10 outcomes, open for 30 to 1800 seconds.</summary>
    Task StartPredictionAsync(string title, IEnumerable<string> outcomes, int seconds);

    /// <summary>Locks the open prediction (no more predicting).</summary>
    Task LockPredictionAsync();

    /// <summary>Resolves the prediction: the outcome with this title (case ignored) wins and points are paid out.</summary>
    Task ResolvePredictionAsync(string winner);

    /// <summary>Cancels the prediction, points go back.</summary>
    Task CancelPredictionAsync();



    // channel point rewards (done as you)



    /// <summary>A channel point reward by its title (case ignored) or id, e.g. <c>Parameters["reward"]</c> in a redeem.</summary>
    Task<TwitchReward> GetRewardAsync(string reward);

    /// <summary>
    /// Changes a reward's settings (only the ones set in <paramref name="changes"/>). Twitch only lets the app that
    /// created a reward change it: create it with New redeem in the Events tab.
    /// </summary>
    Task UpdateRewardAsync(string reward, RewardChanges changes);



    // chat commands (the Editable Commands blocks)



    /// <summary>
    /// Adds a command that replies with <paramref name="response"/> to the first Editable Commands block it fits.
    /// Throws a <see cref="ChatCommandException"/> if it already exists or nothing fits it.
    /// </summary>
    void AddCommand(string command, string response);

    /// <summary>Changes what a command in an Editable Commands block replies (only one that just sends a message).</summary>
    void EditCommand(string command, string response);

    /// <summary>Deletes a command from an Editable Commands block.</summary>
    void DeleteCommand(string command);
}



/// <summary>Where a chat message goes during a shared chat (outside one, it's just your chat either way).</summary>
public enum SharedChat
{
    /// <summary>Only your own chat sees it.</summary>
    ThisChannelOnly,

    /// <summary>Every channel in the shared chat sees it.</summary>
    AllChannels,
}



/// <summary>An announcement's colour.</summary>
public enum AnnouncementColor { Primary, Blue, Green, Orange, Purple }



/// <summary>A chat setting that can be on or off.</summary>
public enum ChatMode { SlowMode, FollowersOnly, SubscribersOnly, EmoteOnly, UniqueChat }



/// <summary>A chat message the app saw, with the same parameters a chat command gets.</summary>
public sealed record ChatMessage(IReadOnlyDictionary<string, string> Parameters)
{
    /// <summary>The message's id.</summary>
    public string Id => Get("messageId");

    /// <summary>The chatter's display name.</summary>
    public string User => Get("user");

    /// <summary>The chatter's login name.</summary>
    public string UserLogin => Get("userLogin");

    /// <summary>The whole message.</summary>
    public string Text => Get("message");

    private string Get(string name) => Parameters.TryGetValue(name, out var value) ? value : "";
}



/// <summary>A channel's title and category (what it's set to, live or not).</summary>
public sealed record TwitchChannel(string Title, string Category);



/// <summary>A stream that's live right now.</summary>
public sealed record TwitchLiveStream(int Viewers, DateTime StartedAt)
{
    /// <summary>How long it has been live.</summary>
    public TimeSpan Uptime => DateTime.UtcNow - StartedAt.ToUniversalTime();
}



/// <summary>A Twitch account.</summary>
public sealed record TwitchUser(string Id, string Login, string DisplayName, DateTime CreatedAt, string Description);



/// <summary>One of a channel's clips.</summary>
public sealed record TwitchClip(string Id, string Url, string Title, string Channel, double Seconds, string Thumbnail, int Views);



/// <summary>A channel point reward. The limits are 0 when they're off.</summary>
public sealed record TwitchReward(
    string Id,
    string Title,
    int Cost,
    string Prompt,
    bool IsEnabled,
    bool IsPaused,
    bool IsUserInputRequired,
    bool SkipsRequestQueue,
    int CooldownSeconds,
    int MaxPerStream,
    int MaxPerViewerPerStream,
    string BackgroundColor);



/// <summary>Changes to a reward: only the ones that aren't null are changed. A limit of 0 turns it off.</summary>
public sealed class RewardChanges
{
    public string? Title { get; init; }
    public int? Cost { get; init; }
    public string? Prompt { get; init; }
    public bool? IsEnabled { get; init; }
    public bool? IsPaused { get; init; }
    public bool? IsUserInputRequired { get; init; }
    public bool? SkipsRequestQueue { get; init; }
    public int? CooldownSeconds { get; init; }
    public int? MaxPerStream { get; init; }
    public int? MaxPerViewerPerStream { get; init; }
    public string? BackgroundColor { get; init; }
}



/// <summary>A chat command couldn't be added, changed or deleted; <see cref="ChatReply"/> is a short reason for chat.</summary>
public sealed class ChatCommandException(string message, string? chatReply) : StreamAssistException(message)
{
    /// <summary>A short reason to tell chat (e.g. "command already exists"), null for a mistake in the command itself.</summary>
    public string? ChatReply { get; } = chatReply;
}
