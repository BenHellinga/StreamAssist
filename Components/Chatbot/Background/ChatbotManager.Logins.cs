using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Core.Connections;

namespace StreamAssist.Components.Chatbot.Background;



// keeping both twitch logins working: the background check, refreshing a token, and what happens when twitch refuses one
public partial class ChatbotManager
{
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

            SetStatus(ConnectionStatus.Failed, "Login expired");
            AppLog.WriteWarning("streamer login expired, press retry");
            return;
        }

        if (_bot == null) return;

        _botLogin?.Cancel();
        _botLogin = null;
        _bot = null;

        SetBotStatus(ConnectionStatus.Failed, "Login expired");
        AppLog.WriteWarning("bot login expired, press retry");
    });



    // twitch refused an action's token: stop using that account (its saved login is kept, so Retry uses it again
    // without a new code), say it disconnected once (later actions just say it isn't connected), then the action's error
    private void OnLoginRefused(TwitchAccount account, TwitchSession session, string what) => Dispatcher.UIThread.Post(() =>
    {
        if (account == TwitchAccount.Streamer && _streamer == session)
        {
            _run?.Cancel();
            _run?.Dispose();
            _run = null;
            _streamer = null;

            SetStatus(ConnectionStatus.Failed, "Disconnected");
            AppLog.WriteWarning("streamer disconnected from twitch (login refused)");
        }
        else if (account == TwitchAccount.Bot && _bot == session)
        {
            _botLogin?.Cancel();
            _botLogin = null;
            _bot = null;

            SetBotStatus(ConnectionStatus.Failed, "Disconnected");
            AppLog.WriteWarning("bot disconnected from twitch (login refused)");
        }

        AppLog.WriteError($"can't {what} (login refused)");
    });



    private TwitchSession? SessionFor(TwitchAccount account) => account == TwitchAccount.Streamer ? _streamer : _bot;



    private void SetSession(TwitchAccount account, TwitchSession session)
    {
        if (account == TwitchAccount.Streamer) _streamer = session;
        else _bot = session;
    }
}
