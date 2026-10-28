using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Chatbot.Background;



public enum ChatbotStatus { Disconnected, WaitingForLogin, Connecting, Connected, Error }



// keeps the chatbot connected while the model says it should be, and turns twitch events into app events
// the streamer login listens (twitch only sends redeems/subs/bits to the broadcaster), the bot login talks
// everything this raises happens on the UI thread, so listeners can touch models directly
public class ChatbotManager : IDisposable
{
    // private



    private readonly ChatbotComponentModel _model;
    private readonly TwitchAuthManager _auth;

    private CancellationTokenSource? _run;
    private CancellationTokenSource? _botLogin;
    private bool _restartQueued;
    private string? _lastLoggedError; // so the same connection error isn't repeated on every retry

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
        _run = null;
        _botLogin = null;
        _restartQueued = false;

        Status = ChatbotStatus.Disconnected;
        StatusMessage = "Not connected";
        BotStatusMessage = "Not connected";

        _model.ConnectionChanged += OnConnectionChanged;
        _model.CredentialsChanged += OnCredentialsChanged;
        _auth.LoginCodeReady += OnLoginCodeReady;

        if (_model.IsEnabled) Start();
        ResumeBot();
    }



    // public api



    // sends a message to the streamer's channel, as the bot if one is logged in, otherwise as the streamer
    public async Task<bool> TrySendChatMessageAsync(string message)
    {
        if (_streamer == null)
        {
            AppLog.WriteError("Could not send chat message - chatbot not connected");
            return false;
        }

        try
        {
            await TwitchApi.SendChatMessageAsync(_bot ?? _streamer, _streamer.UserId, message, CancellationToken.None);
            return true;
        }
        catch (Exception ex)
        {
            AppLog.WriteError($"Could not send chat message - {ex.Message}");
            return false;
        }
    }



    // logs the bot account in (shows a code to enter in a private browser window, logged in as the bot)
    public void StartBotLogin()
    {
        if (string.IsNullOrWhiteSpace(_model.ClientId) || string.IsNullOrWhiteSpace(_model.ClientSecret))
        {
            AppLog.WriteError("Could not log in bot - enter the Client ID and Client Secret first");
            return;
        }

        _botLogin?.Cancel();
        _botLogin = new CancellationTokenSource();
        SetBotStatus("Logging in...");
        _ = LogInBotAsync(_model.ClientId, _model.ClientSecret, interactive: true, _botLogin.Token);
    }



    // forget an account's saved login
    public void LogOut(TwitchAccount account)
    {
        _auth.LogOut(account);

        if (account == TwitchAccount.Bot)
        {
            _botLogin?.Cancel();
            _bot = null;
            SetBotStatus("Not connected");
        }
        else if (_model.IsEnabled) Restart();
        else
        {
            _streamer = null;
            RaiseStatusChanged();
        }
    }



    // model callbacks



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



    // the app changed: drop the old bot login and quietly use a saved one for the new app if there is one
    private void OnCredentialsChanged()
    {
        _botLogin?.Cancel();
        _bot = null;
        SetBotStatus("Not connected");
        ResumeBot();
    }



    // bot



    // uses the bot's saved login without asking for a code (the Log in button does that)
    private void ResumeBot()
    {
        if (string.IsNullOrWhiteSpace(_model.ClientId) || string.IsNullOrWhiteSpace(_model.ClientSecret)) return;

        _botLogin?.Cancel();
        _botLogin = new CancellationTokenSource();
        _ = LogInBotAsync(_model.ClientId, _model.ClientSecret, interactive: false, _botLogin.Token);
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



    // connects, and keeps reconnecting (waiting longer each time) until stopped
    private async Task RunAsync(string clientId, string clientSecret, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            SetStatus(ChatbotStatus.Error, "Not connected", "Could not connect to Twitch - enter the Client ID and Client Secret first");
            return;
        }

        var retryDelay = TimeSpan.FromSeconds(5);

        while (!ct.IsCancellationRequested)
        {
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
                // the saved login stopped working, the next attempt logs in again
                _auth.LogOut(TwitchAccount.Streamer);
                SetStatus(ChatbotStatus.Error, "Reconnecting...", $"Could not connect to Twitch - login expired ({ex.Message}), logging in again");
            }
            catch (Exception ex)
            {
                SetStatus(ChatbotStatus.Error, "Reconnecting...", $"Could not connect to Twitch - {ex.Message}, retrying in {retryDelay.TotalSeconds:0}s");
            }

            try { await Task.Delay(retryDelay, ct); }
            catch (OperationCanceledException) { return; }

            retryDelay = TimeSpan.FromSeconds(Math.Min(retryDelay.TotalSeconds * 2, 60));
        }
    }



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

            if (bot == null || ct.IsCancellationRequested) return;

            Dispatcher.UIThread.Post(() =>
            {
                _bot = bot;
                if (bot.UserId == _streamer?.UserId)
                    AppLog.WriteWarning("Could not use a separate bot account - the bot was approved as the streamer, log the bot out and log in again in a private browser window as the bot");

                SetBotStatus($"Connected as {bot.Login}");
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            SetBotStatus("Login failed");
            AppLog.WriteError($"Could not log in bot - {ex.Message}");
        }
    }



    // twitch callbacks



    // the link already has the code in it, so the user just opens it and authorizes
    private void OnLoginCodeReady(TwitchAccount account, string code, string url)
    {
        if (account == TwitchAccount.Bot)
        {
            // the default browser is probably logged in as the streamer, so the bot needs a private window
            SetBotStatus("Waiting for login code");
            AppLog.WriteLine($"Bot login - in a private/incognito browser window logged in as the bot, go to {url} and authorize");
            return;
        }

        SetStatus(ChatbotStatus.WaitingForLogin, "Waiting for login code");
        AppLog.WriteLine($"Streamer login - go to {url} and authorize");
    }



    // runs on the UI thread
    private void OnEventReceived(TwitchEvent e) => EventReceived?.Invoke(e);



    // helpers



    // message is the short state shown beside the button, log (if any) is the detail for the console
    // can be called from any thread, listeners always get it on the UI thread
    private void SetStatus(ChatbotStatus status, string message, string? log = null)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // the retry delay changes every attempt, compare without it so a long outage is reported once
            var logKey = log == null ? null : Regex.Replace(log, @"retrying in \d+s", "");
            if (logKey != null && logKey != _lastLoggedError) AppLog.WriteError(log!);
            _lastLoggedError = status == ChatbotStatus.Connected ? null : logKey ?? _lastLoggedError;

            if (status == Status && message == StatusMessage) return;

            Status = status;
            StatusMessage = message;
            StatusChanged?.Invoke();
        });
    }



    // can be called from any thread, listeners always get it on the UI thread
    private void SetBotStatus(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (message == BotStatusMessage) return;

            BotStatusMessage = message;
            StatusChanged?.Invoke();
        });
    }



    private void RaiseStatusChanged() => StatusChanged?.Invoke();



    // disposal



    public void Dispose()
    {
        _model.ConnectionChanged -= OnConnectionChanged;
        _model.CredentialsChanged -= OnCredentialsChanged;
        _auth.LoginCodeReady -= OnLoginCodeReady;

        _botLogin?.Cancel();
        _run?.Cancel();
        _run?.Dispose();
        _run = null;
    }
}
