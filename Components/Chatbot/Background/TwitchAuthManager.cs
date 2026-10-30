using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using StreamAssist.Core.Persistence;

namespace StreamAssist.Components.Chatbot.Background;



// the streamer account listens (events only go to the broadcaster), the bot account talks (optional)
public enum TwitchAccount { Streamer, Bot }



// a usable login: which app, which token, who it belongs to, and when twitch said the token runs out (a few hours
// after it was made, then it's refreshed with TryRefreshAsync)
public sealed record TwitchSession(string ClientId, string ClientSecret, string AccessToken, string UserId, string Login, DateTime ExpiresAt);



// one account's saved tokens (they only work with the app that made them)
public class TwitchTokenData
{
    [JsonPropertyName("clientId")] public string ClientId { get; set; } = "";
    [JsonPropertyName("accessToken")] public string AccessToken { get; set; } = "";
    [JsonPropertyName("refreshToken")] public string RefreshToken { get; set; } = "";
}



// the "twitchLogins" section of Config/auth.json (git ignored, never share it, it's a login)
public class TwitchAuthData
{
    [JsonPropertyName("streamer")] public TwitchTokenData Streamer { get; set; } = new();
    [JsonPropertyName("bot")] public TwitchTokenData Bot { get; set; } = new();

    public TwitchTokenData For(TwitchAccount account) => account == TwitchAccount.Streamer ? Streamer : Bot;
}



// logs in to twitch with the device code flow (the user types a code at twitch.tv/activate),
// remembers each account's token, and refreshes them when they expire
public class TwitchAuthManager
{
    // private



    private const string Section = "twitchLogins";
    private readonly object _fileLock = new(); // both accounts can be logging in at the same time



    // public



    // what each account asks permission for (events that need a missing scope just get skipped)
    public static string[] ScopesFor(TwitchAccount account) => account == TwitchAccount.Streamer
        ?
        [
            "user:read:chat",                  // read chat messages and chat notifications
            "user:write:chat",                 // send chat when there's no bot account
            "channel:read:redemptions",        // channel point redeems
            "channel:manage:redemptions",      // read, change and create channel point rewards (Get / Set Reward Setting, New reward)
            "moderator:manage:announcements",  // Send Announcement
            "moderator:manage:chat_settings",  // Set Chat Mode
            "moderator:manage:shoutouts",      // Shoutout
            "channel:manage:broadcast",        // Set Stream Info, Create Stream Marker
            "clips:edit",                      // Create Clip
            "channel:edit:commercial",         // Run Ad
            "channel:manage:polls",            // Start / End Poll
            "channel:manage:predictions",      // Start / End Prediction
            "channel:manage:vips",             // Set VIP
            "channel:manage:moderators",       // Set Moderator
            "channel:read:subscriptions",      // subs, resubs, gift subs
            "bits:read",                       // cheers
            "moderator:read:followers",        // follows
            "moderator:manage:banned_users",   // ban / timeout
            "moderator:manage:chat_messages",  // delete messages
        ]
        :
        [
            "user:write:chat",                 // send chat
            "moderator:manage:banned_users",   // ban / timeout (the bot must be a mod)
            "moderator:manage:chat_messages",  // delete messages (the bot must be a mod)
            "moderator:manage:announcements",  // announcements (the bot must be a mod)
            "moderator:manage:chat_settings",  // slow mode, followers only, ... (the bot must be a mod)
        ];



    // events



    // raised from a background thread when the user needs to enter a code (account, code, url)
    public event Action<TwitchAccount, string, string>? LoginCodeReady;



    // public api



    // uses the saved login if it still works, otherwise starts a new code login
    public async Task<TwitchSession> LogInAsync(TwitchAccount account, string clientId, string clientSecret, CancellationToken ct)
    {
        var session = await TryResumeAsync(account, clientId, clientSecret, ct);
        return session ?? await DeviceLoginAsync(account, clientId, clientSecret, ct);
    }



    // the saved login if it still works (refreshing it if needed), null if a new code login is needed
    public async Task<TwitchSession?> TryResumeAsync(TwitchAccount account, string clientId, string clientSecret, CancellationToken ct)
    {
        var tokens = Load().For(account);
        if (tokens.ClientId != clientId || tokens.AccessToken == "") return null;

        var info = await TwitchApi.ValidateAsync(tokens.AccessToken, ct);

        // expired: swap the refresh token for a new one
        if (info == null)
        {
            if (tokens.RefreshToken == "") return null;

            try
            {
                var token = await TwitchApi.RefreshAsync(clientId, clientSecret, tokens.RefreshToken, ct);
                tokens = Save(account, clientId, token);
            }
            catch (TwitchUnauthorizedException)
            {
                return null;
            }

            info = await TwitchApi.ValidateAsync(tokens.AccessToken, ct);
            if (info == null) return null;
        }

        // the scope list changed since this login was made, log in again to get the new permissions
        var granted = info.Value.GetProperty("scopes").EnumerateArray().Select(s => s.GetString()).ToHashSet();
        if (!ScopesFor(account).All(granted.Contains)) return null;

        return ToSession(clientId, clientSecret, tokens, info.Value);
    }



    // a new token for a login that's in use (before it runs out, or after twitch refused it), saved for next time
    // null if twitch refused the refresh token: the login is gone and a new code login is needed. Other problems (e.g.
    // twitch being down) throw, so the caller can try again later without losing the login
    public async Task<TwitchSession?> TryRefreshAsync(TwitchAccount account, TwitchSession session, CancellationToken ct)
    {
        var tokens = Load().For(account);
        if (tokens.ClientId != session.ClientId || tokens.RefreshToken == "") return null;

        try
        {
            var token = await TwitchApi.RefreshAsync(session.ClientId, session.ClientSecret, tokens.RefreshToken, ct);
            tokens = Save(account, session.ClientId, token);
        }
        catch (TwitchUnauthorizedException)
        {
            return null;
        }

        var info = await TwitchApi.ValidateAsync(tokens.AccessToken, ct);
        return info == null ? null : ToSession(session.ClientId, session.ClientSecret, tokens, info.Value);
    }



    // asks twitch whether the token still works (twitch wants apps to check about once an hour), with its new expiry,
    // null if it no longer works
    public static async Task<TwitchSession?> TryValidateAsync(TwitchSession session, CancellationToken ct)
    {
        var info = await TwitchApi.ValidateAsync(session.AccessToken, ct);
        return info == null ? null : session with { ExpiresAt = ExpiresAt(info.Value) };
    }



    // forget an account's saved login (the next login asks for a new code)
    public void LogOut(TwitchAccount account)
    {
        lock (_fileLock)
        {
            var data = Load();
            var tokens = data.For(account);
            tokens.AccessToken = "";
            tokens.RefreshToken = "";
            CredentialStore.Save(Section, data);
        }
    }



    // helpers



    private async Task<TwitchSession> DeviceLoginAsync(TwitchAccount account, string clientId, string clientSecret, CancellationToken ct)
    {
        var scopes = ScopesFor(account);
        var start = await TwitchApi.StartDeviceLoginAsync(clientId, scopes, ct);

        var deviceCode = start.GetProperty("device_code").GetString()!;
        var userCode = start.GetProperty("user_code").GetString()!;
        var url = start.GetProperty("verification_uri").GetString()!;
        var interval = TimeSpan.FromSeconds(start.GetProperty("interval").GetInt32());
        var deadline = DateTime.UtcNow.AddSeconds(start.GetProperty("expires_in").GetInt32());

        LoginCodeReady?.Invoke(account, userCode, url);

        // twitch says how often we're allowed to check if the code has been entered
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(interval, ct);

            var token = await TwitchApi.PollDeviceLoginAsync(clientId, clientSecret, deviceCode, scopes, ct);
            if (token == null) continue;

            var tokens = Save(account, clientId, token.Value);
            var info = await TwitchApi.ValidateAsync(tokens.AccessToken, ct)
                ?? throw new Exception("Twitch gave a login that doesn't work, try again");

            return ToSession(clientId, clientSecret, tokens, info);
        }

        throw new Exception("The login code expired before it was entered, try again to get a new one");
    }



    private TwitchAuthData Load()
    {
        lock (_fileLock) return CredentialStore.Load<TwitchAuthData>(Section);
    }



    private TwitchTokenData Save(TwitchAccount account, string clientId, JsonElement token)
    {
        lock (_fileLock)
        {
            var data = CredentialStore.Load<TwitchAuthData>(Section);

            var tokens = data.For(account);
            tokens.ClientId = clientId;
            tokens.AccessToken = token.GetProperty("access_token").GetString()!;
            tokens.RefreshToken = token.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() ?? "" : "";

            CredentialStore.Save(Section, data);
            return tokens;
        }
    }



    private static TwitchSession ToSession(string clientId, string clientSecret, TwitchTokenData tokens, JsonElement info) => new(
        clientId,
        clientSecret,
        tokens.AccessToken,
        info.GetProperty("user_id").GetString()!,
        info.GetProperty("login").GetString()!,
        ExpiresAt(info));



    // validate says how many seconds the token has left (0 for tokens that don't expire)
    private static DateTime ExpiresAt(JsonElement info) =>
        info.TryGetProperty("expires_in", out var seconds) && seconds.GetInt64() > 0
            ? DateTime.UtcNow.AddSeconds(seconds.GetInt64())
            : DateTime.MaxValue;
}
