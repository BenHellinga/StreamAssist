using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Core.Connections;

namespace StreamAssist.Components.Chatbot.Background;



public enum ChatbotStatus { Disconnected, WaitingForLogin, Connecting, Connected, Error }



// keeps the chatbot connected while the model says it should be, and turns twitch events into app events
// the streamer login listens (twitch only sends redeems/subs/bits to the broadcaster), the bot login talks
// everything this raises happens on the UI thread, so listeners can touch models directly
public class ChatbotManager : IDisposable
{
    // private



    // at most one twitch action (chat message, timeout, ban, delete) this often, so busy chat can't make us spam twitch
    private static readonly TimeSpan ActionInterval = TimeSpan.FromSeconds(1);

    // twitch won't post the same message twice in a row, this (a space and an invisible combining grapheme joiner,
    // U+034F) at the end makes a repeat different without changing how it looks
    private const string DuplicateSuffix = " \u034F";

    // twitch logins run out after a few hours: both are checked this often (twitch wants apps to check about hourly),
    // and one with less than RefreshBefore left gets a new token before anything fails
    private static readonly TimeSpan LoginCheckInterval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan RefreshBefore = TimeSpan.FromMinutes(45);

    private readonly ChatbotComponentModel _model;
    private readonly TwitchAuthManager _auth;
    private readonly TwitchActionQueue _actions;
    private string? _lastSentMessage; // exactly what went out last (only touched inside the action queue, one at a time)

    private CancellationTokenSource? _run;
    private CancellationTokenSource? _botLogin;
    private bool _restartQueued;
    private readonly CancellationTokenSource _lifetime; // stops the login checks when the app closes
    private readonly SemaphoreSlim _refreshLock; // one token refresh at a time

    private TwitchSession? _streamer;
    private TwitchSession? _bot;



    // every event type the chatbot listens for: (type, version, condition from the broadcaster's id)
    private static readonly (string Type, string Version, Func<string, Dictionary<string, string>> Condition)[] Subscriptions =
    [
        (TwitchEvent.ChatMessage,       "1", me => new() { ["broadcaster_user_id"] = me, ["user_id"] = me }),
        (TwitchEvent.ChatNotification,  "1", me => new() { ["broadcaster_user_id"] = me, ["user_id"] = me }),
        (TwitchEvent.Redemption,        "1", me => new() { ["broadcaster_user_id"] = me }),
        (TwitchEvent.Subscribe,         "1", me => new() { ["broadcaster_user_id"] = me }),
        (TwitchEvent.SubscriptionGift,  "1", me => new() { ["broadcaster_user_id"] = me }),
        (TwitchEvent.Resubscribe,       "1", me => new() { ["broadcaster_user_id"] = me }),
        (TwitchEvent.Cheer,             "1", me => new() { ["broadcaster_user_id"] = me }),
        (TwitchEvent.Raid,              "1", me => new() { ["to_broadcaster_user_id"] = me }),
        (TwitchEvent.Follow,            "2", me => new() { ["broadcaster_user_id"] = me, ["moderator_user_id"] = me }),
        (TwitchEvent.StreamOnline,      "1", me => new() { ["broadcaster_user_id"] = me }),
        (TwitchEvent.StreamOffline,     "1", me => new() { ["broadcaster_user_id"] = me }),
    ];



    // public



    public ChatbotStatus Status { get; private set; }
    public string StatusMessage { get; private set; } // the streamer connection
    public string BotStatusMessage { get; private set; }
    public ConnectionStatus BotConnection { get; private set; }

    // the streamer's status as the Connections tab shows it
    public ConnectionStatus StreamerConnection => Status switch
    {
        ChatbotStatus.Connected => ConnectionStatus.Connected,
        ChatbotStatus.Connecting or ChatbotStatus.WaitingForLogin => ConnectionStatus.Connecting,
        ChatbotStatus.Error => ConnectionStatus.Failed,
        _ => ConnectionStatus.NotConnected,
    };
    public string? StreamerLogin => _streamer?.Login;
    public string? BotLogin => _bot?.Login;
    public string? BotUserId => _bot?.UserId;



    // events



    public event Action? StatusChanged; // either account's status or login changed
    public event Action<TwitchEvent>? EventReceived;



    // constructor



    public ChatbotManager(ChatbotComponentModel model)
    {
        _model = model;
        _auth = new();
        _actions = new(ActionInterval);
        _run = null;
        _botLogin = null;
        _restartQueued = false;
        _lifetime = new();
        _refreshLock = new(1, 1);

        Status = ChatbotStatus.Disconnected;
        StatusMessage = "Not connected";
        BotStatusMessage = "Not connected";
        BotConnection = ConnectionStatus.NotConnected;

        _model.ConnectionChanged += OnConnectionChanged;
        _model.BotConnectionChanged += OnBotConnectionChanged;
        _model.CredentialsChanged += OnCredentialsChanged;
        _model.CreateRewardRequested += OnCreateRewardRequested;
        _auth.LoginCodeReady += OnLoginCodeReady;

        if (_model.IsEnabled) Start();
        if (_model.IsBotEnabled) ResumeBot();

        _ = KeepLoginsFreshAsync(_lifetime.Token);
    }



    // public api



    // twitch actions: each waits its turn in the queue, is done as the bot if one is logged in (otherwise as the
    // streamer; channel point rewards always as the streamer), and logs why if it fails (false), so the block that
    // asked doesn't have to



    // sends a message to the streamer's channel (a repeat of the last one gets an invisible character so twitch posts it)
    public Task<bool> TrySendChatMessageAsync(string message, CancellationToken ct = default) =>
        TryRunActionAsync("send chat message", async (account, channelId, token) =>
        {
            var text = AvoidDuplicate(message, _lastSentMessage);
            await TwitchApi.SendChatMessageAsync(account, channelId, text, token);
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
                throw new Exception("it was made outside StreamAssist, and twitch only lets the app that created a reward change it (create it with New reward in the Events tab)");
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



    // model callbacks



    // the New reward panel asked: create it on twitch, then give it a redeem row to bind (a failure is already logged)
    private async void OnCreateRewardRequested(NewReward reward)
    {
        if (await TryCreateRewardAsync(reward)) _model.Redeems.AddRow(reward.Title);
    }



    // several settings often change together (id + secret + enabled), so restart once after they've all changed
    private void OnConnectionChanged()
    {
        if (_restartQueued) return;

        _restartQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _restartQueued = false;
            Restart();
        });
    }



    // the bot was connected/disconnected, or a reconnect was asked for
    private void OnBotConnectionChanged()
    {
        DropBot();
        if (_model.IsBotEnabled) ConnectBot();
    }



    // the app changed: drop the old bot login and quietly use a saved one for the new app if there is one
    private void OnCredentialsChanged()
    {
        DropBot();
        if (_model.IsBotEnabled) ResumeBot();
    }



    // bot



    // logs the bot in, using its saved login or showing a code to enter in a private browser window, logged in as the bot
    private void ConnectBot()
    {
        if (string.IsNullOrWhiteSpace(_model.ClientId) || string.IsNullOrWhiteSpace(_model.ClientSecret))
        {
            SetBotStatus(ConnectionStatus.Failed, "Not connected");
            AppLog.WriteError("Could not connect bot - enter the Client ID and Client Secret first");
            return;
        }

        _botLogin = new CancellationTokenSource();
        SetBotStatus(ConnectionStatus.Connecting, "Logging in...");
        _ = LogInBotAsync(_model.ClientId, _model.ClientSecret, interactive: true, _botLogin.Token);
    }



    // uses the bot's saved login without asking for a code (at startup, or after the credentials changed)
    private void ResumeBot()
    {
        if (string.IsNullOrWhiteSpace(_model.ClientId) || string.IsNullOrWhiteSpace(_model.ClientSecret)) return;

        _botLogin = new CancellationTokenSource();
        SetBotStatus(ConnectionStatus.Connecting, "Logging in...");
        _ = LogInBotAsync(_model.ClientId, _model.ClientSecret, interactive: false, _botLogin.Token);
    }



    // stops using the bot (its saved login is kept), chat is sent as the streamer
    private void DropBot()
    {
        _botLogin?.Cancel();
        _botLogin = null;
        _bot = null;
        SetBotStatus(ConnectionStatus.NotConnected, "Not connected");
    }



    // connection



    private void Restart()
    {
        Stop();
        if (_model.IsEnabled) Start();
    }



    private void Start()
    {
        _run = new CancellationTokenSource();
        _ = RunAsync(_model.ClientId, _model.ClientSecret, _run.Token);
    }



    private void Stop()
    {
        _run?.Cancel();
        _run?.Dispose();
        _run = null;

        _streamer = null;
        SetStatus(ChatbotStatus.Disconnected, "Not connected");
    }



    // connects once and stays connected until stopped; if it fails (or the connection drops later) it's logged
    // and left failed, the user retries from the Connections tab
    private async Task RunAsync(string clientId, string clientSecret, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            SetStatus(ChatbotStatus.Error, "Not connected", "Could not connect to Twitch - enter the Client ID and Client Secret first");
            return;
        }

        try
        {
            await ConnectOnceAsync(clientId, clientSecret, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }
        catch (TwitchUnauthorizedException ex)
        {
            // the saved login stopped working, forget it so Retry logs in again
            _auth.LogOut(TwitchAccount.Streamer);
            ClearStreamer(ct);
            SetStatus(ChatbotStatus.Error, "Login expired", $"Could not connect to Twitch - login expired ({ex.Message}), press Retry to log in again");
        }
        catch (Exception ex)
        {
            if (ct.IsCancellationRequested) return;

            var wasConnected = Status == ChatbotStatus.Connected;
            ClearStreamer(ct);

            if (!wasConnected)
            {
                SetStatus(ChatbotStatus.Error, "Not connected", $"Could not connect to Twitch - {ex.Message}");
                return;
            }

            // it was working, then twitch went away mid-stream
            SetStatus(ChatbotStatus.Error, "Disconnected");
            AppLog.WriteWarning($"Could not stay connected to Twitch as the streamer - {ex.Message}, press Retry in the Connections tab to reconnect");
        }
    }



    // the failed connection's session can't be used to send chat any more
    private void ClearStreamer(CancellationToken ct) => Dispatcher.UIThread.Post(() =>
    {
        if (ct.IsCancellationRequested) return; // a newer connection owns it now

        _streamer = null;
        RaiseStatusChanged();
    });



    private async Task ConnectOnceAsync(string clientId, string clientSecret, CancellationToken ct)
    {
        SetStatus(ChatbotStatus.Connecting, "Logging in...");
        var streamer = await _auth.LogInAsync(TwitchAccount.Streamer, clientId, clientSecret, ct);

        Dispatcher.UIThread.Post(() =>
        {
            if (ct.IsCancellationRequested) return;
            _streamer = streamer;
            RaiseStatusChanged();
        });

        var client = new TwitchEventSubClient();
        client.SessionStarted += sessionId => _ = SubscribeAllAsync(streamer, sessionId, ct);
        client.EventReceived += e => Dispatcher.UIThread.Post(() =>
        {
            if (!ct.IsCancellationRequested) OnEventReceived(e); // ignore stragglers from a stopped connection
        });
        client.SubscriptionRevoked += (type, reason) => AppLog.WriteWarning($"Could not keep listening for '{type}' - twitch stopped sending it ({reason})");

        SetStatus(ChatbotStatus.Connecting, "Connecting...");
        await client.RunAsync(ct);
    }



    // asks for every event type, ones the account isn't allowed to see are logged and skipped
    private async Task SubscribeAllAsync(TwitchSession streamer, string sessionId, CancellationToken ct)
    {
        var failed = new List<string>();

        foreach (var (type, version, condition) in Subscriptions)
        {
            try
            {
                await TwitchApi.SubscribeAsync(streamer, sessionId, type, version, condition(streamer.UserId), ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                failed.Add($"{type} ({ex.Message})");
            }
        }

        if (ct.IsCancellationRequested) return;

        foreach (var failure in failed)
            AppLog.WriteWarning($"Could not listen for {failure}");

        SetStatus(ChatbotStatus.Connected, $"Connected as {streamer.Login}");
    }



    // interactive: ask for a code if there's no saved login, otherwise only use a saved one
    private async Task LogInBotAsync(string clientId, string clientSecret, bool interactive, CancellationToken ct)
    {
        try
        {
            var bot = interactive
                ? await _auth.LogInAsync(TwitchAccount.Bot, clientId, clientSecret, ct)
                : await _auth.TryResumeAsync(TwitchAccount.Bot, clientId, clientSecret, ct);

            if (ct.IsCancellationRequested) return;

            // no saved login to resume, the Connect button asks for a code
            if (bot == null)
            {
                SetBotStatus(ConnectionStatus.NotConnected, "Not connected");
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                if (ct.IsCancellationRequested) return;

                _bot = bot;
                if (bot.UserId == _streamer?.UserId)
                    AppLog.WriteWarning("Could not use a separate bot account - the bot was approved as the streamer, log in again in a private browser window as the bot");

                SetBotStatus(ConnectionStatus.Connected, $"Connected as {bot.Login}");
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (ct.IsCancellationRequested) return;

            SetBotStatus(ConnectionStatus.Failed, "Login failed");
            AppLog.WriteError($"Could not connect bot - {ex.Message}");
        }
    }



    // twitch callbacks



    // the link already has the code in it, so the user just opens it and authorizes
    private void OnLoginCodeReady(TwitchAccount account, string code, string url)
    {
        if (account == TwitchAccount.Bot)
        {
            // the default browser is probably logged in as the streamer, so the bot needs a private window
            SetBotStatus(ConnectionStatus.Connecting, "Waiting for login code");
            AppLog.WriteLine($"Bot login - in a private/incognito browser window logged in as the bot, go to {url} and authorize");
            return;
        }

        SetStatus(ChatbotStatus.WaitingForLogin, "Waiting for login code");
        AppLog.WriteLine($"Streamer login - go to {url} and authorize");
    }



    // runs on the UI thread
    private void OnEventReceived(TwitchEvent e) => EventReceived?.Invoke(e);



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
    // done as the bot if one is connected, unless asStreamer (things only the broadcaster can do)
    private async Task<(bool Ok, T? Value)> TryRunActionAsync<T>(string what, Func<TwitchSession, string, CancellationToken, Task<T>> action,
        CancellationToken ct, bool asStreamer = false) =>
        await _actions.RunAsync<(bool, T?)>(async () =>
        {
            var streamer = _streamer;
            if (streamer == null)
            {
                AppLog.WriteError($"Could not {what} - chatbot not connected");
                return (false, default);
            }

            var account = asStreamer || _bot == null ? TwitchAccount.Streamer : TwitchAccount.Bot;
            var session = account == TwitchAccount.Streamer ? streamer : _bot!;

            try
            {
                try
                {
                    return (true, await action(session, streamer.UserId, ct));
                }
                catch (TwitchUnauthorizedException)
                {
                    // the token ran out before the regular check caught it: get a new one and try once more
                    var fresh = await TryRefreshSessionAsync(account, session);
                    if (fresh == null) throw;
                    return (true, await action(fresh, streamer.UserId, ct));
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                AppLog.WriteError($"Could not {what} - {ex.Message}");
                return (false, default);
            }
        }, ct);



    // logins



    // checks both logins every LoginCheckInterval until the app closes
    private async Task KeepLoginsFreshAsync(CancellationToken ct)
    {
        while (true)
        {
            try
            {
                await Task.Delay(LoginCheckInterval, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await CheckLoginAsync(TwitchAccount.Streamer, ct);
            await CheckLoginAsync(TwitchAccount.Bot, ct);
        }
    }



    // still fine: keep going (with twitch's new expiry); running out soon or already refused: refresh it now
    private async Task CheckLoginAsync(TwitchAccount account, CancellationToken ct)
    {
        var session = SessionFor(account);
        if (session == null) return;

        try
        {
            var current = await TwitchAuthManager.TryValidateAsync(session, ct);
            if (current != null && current.ExpiresAt - DateTime.UtcNow > RefreshBefore)
            {
                if (SessionFor(account) == session) SetSession(account, current);
                return;
            }

            await TryRefreshSessionAsync(account, session);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            // twitch couldn't be reached right now, the next check tries again (a dropped connection reports itself)
        }
    }



    // a new token for an account whose token ran out (or is about to), one refresh at a time: if another refresh already
    // replaced this token, that one is used. Null when there's no login any more (disconnected, or twitch refused it,
    // which is reported). Twitch being unreachable throws, the login is kept for next time
    private async Task<TwitchSession?> TryRefreshSessionAsync(TwitchAccount account, TwitchSession used)
    {
        await _refreshLock.WaitAsync();
        try
        {
            var current = SessionFor(account);
            if (current == null) return null;
            if (current.AccessToken != used.AccessToken) return current;

            var fresh = await _auth.TryRefreshAsync(account, current, CancellationToken.None);
            if (fresh == null)
            {
                OnLoginLost(account);
                return null;
            }

            if (SessionFor(account) == current) SetSession(account, fresh);
            return fresh;
        }
        finally
        {
            _refreshLock.Release();
        }
    }



    // twitch won't refresh the login any more (e.g. the app's access was removed): forget it, stop using it, and say so
    // once. Retry in the Connections tab starts a new code login
    private void OnLoginLost(TwitchAccount account) => Dispatcher.UIThread.Post(() =>
    {
        _auth.LogOut(account);

        if (account == TwitchAccount.Streamer)
        {
            if (_streamer == null) return;

            _run?.Cancel();
            _run?.Dispose();
            _run = null;
            _streamer = null;

            SetStatus(ChatbotStatus.Error, "Login expired");
            AppLog.WriteWarning("Could not stay logged in to Twitch as the streamer - twitch no longer accepts the login, press Retry in the Connections tab to log in again");
            return;
        }

        if (_bot == null) return;

        _botLogin?.Cancel();
        _botLogin = null;
        _bot = null;

        SetBotStatus(ConnectionStatus.Failed, "Login expired");
        AppLog.WriteWarning("Could not stay logged in to Twitch as the bot - twitch no longer accepts the login, chat is sent as the streamer until you press Retry in the Connections tab");
    });



    private TwitchSession? SessionFor(TwitchAccount account) => account == TwitchAccount.Streamer ? _streamer : _bot;



    private void SetSession(TwitchAccount account, TwitchSession session)
    {
        if (account == TwitchAccount.Streamer) _streamer = session;
        else _bot = session;
    }



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



    // helpers



    // message is the short state shown beside the button, log (if any) is the detail for the console
    // can be called from any thread, listeners always get it on the UI thread
    private void SetStatus(ChatbotStatus status, string message, string? log = null)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (log != null) AppLog.WriteError(log);

            if (status == Status && message == StatusMessage) return;

            Status = status;
            StatusMessage = message;
            StatusChanged?.Invoke();
        });
    }



    // can be called from any thread, listeners always get it on the UI thread
    private void SetBotStatus(ConnectionStatus status, string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (status == BotConnection && message == BotStatusMessage) return;

            BotConnection = status;
            BotStatusMessage = message;
            StatusChanged?.Invoke();
        });
    }



    private void RaiseStatusChanged() => StatusChanged?.Invoke();



    // disposal



    public void Dispose()
    {
        _model.ConnectionChanged -= OnConnectionChanged;
        _model.BotConnectionChanged -= OnBotConnectionChanged;
        _model.CredentialsChanged -= OnCredentialsChanged;
        _model.CreateRewardRequested -= OnCreateRewardRequested;
        _auth.LoginCodeReady -= OnLoginCodeReady;

        _botLogin?.Cancel();
        _run?.Cancel();
        _run?.Dispose();
        _run = null;

        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}
