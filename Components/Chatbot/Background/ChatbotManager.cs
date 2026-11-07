using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Core.Connections;
using StreamAssist.Core.Persistence;

namespace StreamAssist.Components.Chatbot.Background;



// keeps the chatbot connected while the model says it should be, and turns twitch events into app events
// the streamer login listens (twitch only sends redeems/subs/bits to the broadcaster), the bot login talks
// everything this raises happens on the UI thread, so listeners can touch models directly
// split over files: this one connects, ChatbotManager.Actions.cs runs twitch actions, ChatbotManager.Logins.cs keeps logins fresh
public partial class ChatbotManager : IDisposable
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



    // each account's status as the Connections tab shows it, with a short detail (e.g. "Waiting for login code")
    public ConnectionStatus StreamerConnection { get; private set; }
    public string StatusMessage { get; private set; }
    public ConnectionStatus BotConnection { get; private set; }
    public string BotStatusMessage { get; private set; }
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

        StreamerConnection = ConnectionStatus.NotConnected;
        StatusMessage = "Not connected";
        BotStatusMessage = "Not connected";
        BotConnection = ConnectionStatus.NotConnected;

        _model.ConnectionChanged += OnConnectionChanged;
        _model.BotConnectionChanged += OnBotConnectionChanged;
        _model.CredentialsChanged += OnCredentialsChanged;
        _model.CreateRewardRequested += OnCreateRewardRequested;
        _auth.LoginCodeReady += OnLoginCodeReady;

        if (_model.IsEnabled) Start();
        if (_model.IsBotEnabled) ConnectBot();

        _ = KeepLoginsFreshAsync(_lifetime.Token);
    }



    // model callbacks



    // the New redeem panel asked: create it on twitch, then give it a redeem row to bind (a failure is already logged)
    private async void OnCreateRewardRequested(NewReward reward)
    {
        if (!await TryCreateRewardAsync(reward)) return;
        if (_model.Redeems is { } redeems) redeems.AddRow(reward.Title);
        else AppLog.WriteWarning($"reward '{reward.Title}' made, no events config for its redeem"); // all deleted meanwhile
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



    // the app changed: drop the old bot login and connect again with the new one (like the streamer)
    private void OnCredentialsChanged()
    {
        DropBot();
        if (_model.IsBotEnabled) ConnectBot();
    }



    // bot



    // logs the bot in, using its saved login or showing a login link (opened while logged in to twitch as the bot)
    // the same as the streamer, and on its own: neither waits for the other
    private void ConnectBot()
    {
        if (!CredentialStore.IsReadable)
        {
            SetBotStatus(ConnectionStatus.Failed, "Not connected");
            AppLog.WriteError("can't connect the bot, auth.json can't be read");
            return;
        }

        if (string.IsNullOrWhiteSpace(_model.ClientId) || string.IsNullOrWhiteSpace(_model.ClientSecret))
        {
            SetBotStatus(ConnectionStatus.Failed, "Not connected");
            AppLog.WriteError("bot needs the client id and secret");
            return;
        }

        _botLogin = new CancellationTokenSource();
        SetBotStatus(ConnectionStatus.Connecting, "Logging in...");
        _ = LogInBotAsync(_model.ClientId, _model.ClientSecret, _botLogin.Token);
    }



    // stops using the bot (its saved login is kept), chat is sent as the streamer unless "chat only as bot" is ticked
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
        SetStatus(ConnectionStatus.NotConnected, "Not connected");
    }



    // connects once and stays connected until stopped; if it fails (or the connection drops later) it's logged
    // and left failed, the user retries from the Connections tab
    private async Task RunAsync(string clientId, string clientSecret, CancellationToken ct)
    {
        if (!CredentialStore.IsReadable)
        {
            SetStatus(ConnectionStatus.Failed, "Not connected", "can't connect to twitch, auth.json can't be read");
            return;
        }

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            SetStatus(ConnectionStatus.Failed, "Not connected", "twitch needs the client id and secret");
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
        catch (TwitchUnauthorizedException)
        {
            // the saved login stopped working, forget it so Retry logs in again
            _auth.LogOut(TwitchAccount.Streamer);
            ClearStreamer(ct);
            SetStatus(ConnectionStatus.Failed, "Login expired", "streamer login expired, press retry");
        }
        catch (Exception ex)
        {
            if (ct.IsCancellationRequested) return;

            var wasConnected = StreamerConnection == ConnectionStatus.Connected;
            ClearStreamer(ct);

            if (!wasConnected)
            {
                SetStatus(ConnectionStatus.Failed, "Not connected", $"can't connect to twitch ({ex.Message})");
                return;
            }

            // it was working, then twitch went away mid-stream
            SetStatus(ConnectionStatus.Failed, "Disconnected");
            AppLog.WriteWarning($"streamer disconnected from twitch ({ex.Message})");
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
        SetStatus(ConnectionStatus.Connecting, "Logging in...");
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
        client.SubscriptionRevoked += (type, reason) => AppLog.WriteWarning($"twitch stopped sending {type} ({reason})");

        SetStatus(ConnectionStatus.Connecting, "Connecting...");
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
            AppLog.WriteWarning($"can't listen for {failure}");

        SetStatus(ConnectionStatus.Connected, $"Connected to {streamer.Login}");
    }



    // the saved login, or a login link if there isn't one that still works
    private async Task LogInBotAsync(string clientId, string clientSecret, CancellationToken ct)
    {
        try
        {
            var bot = await _auth.LogInAsync(TwitchAccount.Bot, clientId, clientSecret, ct);
            if (ct.IsCancellationRequested) return;

            Dispatcher.UIThread.Post(() =>
            {
                if (ct.IsCancellationRequested) return;

                _bot = bot;
                if (bot.UserId == _streamer?.UserId)
                    AppLog.WriteWarning("bot logged in as streamer, redo in a private window");

                SetBotStatus(ConnectionStatus.Connected, $"Connected to {bot.Login}");
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (ct.IsCancellationRequested) return;

            SetBotStatus(ConnectionStatus.Failed, "Login failed");
            AppLog.WriteError($"bot login failed ({ex.Message})");
        }
    }



    // twitch callbacks



    // the link already has the code in it, so the user just opens it and authorizes
    private void OnLoginCodeReady(TwitchAccount account, string code, string url)
    {
        if (account == TwitchAccount.Bot)
        {
            SetBotStatus(ConnectionStatus.Connecting, "Waiting for login code");
            AppLog.WritePrompt($"bot login, open: {url}");
            return;
        }

        SetStatus(ConnectionStatus.Connecting, "Waiting for login code");
        AppLog.WritePrompt($"streamer login, open: {url}");
    }



    // runs on the UI thread
    private void OnEventReceived(TwitchEvent e) => EventReceived?.Invoke(e);



    // helpers



    // message is the short state shown beside the button, log (if any) is the detail for the console
    // can be called from any thread, listeners always get it on the UI thread
    private void SetStatus(ConnectionStatus status, string message, string? log = null)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (log != null) AppLog.WriteError(log);

            if (status == StreamerConnection && message == StatusMessage) return;

            StreamerConnection = status;
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
