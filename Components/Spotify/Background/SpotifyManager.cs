using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Components.Spotify.Model;
using StreamAssist.Core.Connections;
using StreamAssist.Core.Persistence;

namespace StreamAssist.Components.Spotify.Background;



// the "spotifyLogin" section of Config/auth.json: the tokens, and which app (client id) made them
public class SpotifyLoginData
{
    [JsonPropertyName("clientId")] public string ClientId { get; set; } = "";
    [JsonPropertyName("accessToken")] public string AccessToken { get; set; } = "";
    [JsonPropertyName("refreshToken")] public string RefreshToken { get; set; } = "";
    [JsonPropertyName("expiresAt")] public DateTime ExpiresAt { get; set; }
}



// where the login is kept (auth.json, the tests keep it in memory instead)
public class SpotifyLoginStore
{
    private const string Section = "spotifyLogin";

    public virtual bool IsReadable => CredentialStore.IsReadable;

    public virtual SpotifyLoginData Load() => CredentialStore.Load<SpotifyLoginData>(Section);

    public virtual void Save(SpotifyLoginData login) => CredentialStore.Save(Section, login);
}



// what Play Spotify looks for when it's given words instead of a link
public enum SpotifyFind { Song, Playlist, Album }



// keeps the Spotify login the model asks for and does what the Spotify blocks ask. Connecting is one attempt like every
// connection: it uses the saved login, or the first time shows a login link in the console and waits for the browser
// to come back. Access tokens last an hour, so a background check every few minutes refreshes one with under 15 minutes
// left; only spotify refusing the refresh token counts as the login being gone. Requests never refresh or retry: one
// whose token is refused marks spotify disconnected (Retry uses the saved login again). Each request logs its own failure and
// returns false, so the block that asked doesn't have to. Songs, playlists and albums are given as a link
// (open.spotify.com/... or spotify:...) or as words to search for
public class SpotifyManager : IDisposable
{
    // private



    private static readonly Regex LinkPattern = new(
        @"^(?:spotify:(?<type>track|album|playlist|artist):(?<id>[A-Za-z0-9]+)|(?:https?://)?open\.spotify\.com/(?:intl-[a-zA-Z-]+/)?(?<type>track|album|playlist|artist)/(?<id>[A-Za-z0-9]+)(?:[?#/].*)?)$",
        RegexOptions.IgnoreCase);

    private static readonly TimeSpan DefaultCheckInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RefreshBefore = TimeSpan.FromMinutes(15);

    private readonly SpotifyComponentModel _model;
    private readonly SpotifyLoginStore _store;
    private readonly CancellationTokenSource _lifetime; // the background login check, until disposed
    private CancellationTokenSource? _run;
    private SpotifyLoginData? _session; // set while connected



    // public



    public ConnectionStatus Status { get; private set; }
    public string StatusMessage { get; private set; } // short, e.g. "Connected to budder"
    public bool IsConnected => _session != null;



    // events



    public event Action? StatusChanged; // always on the UI thread



    // constructor



    // checkInterval: how often the login is checked (the tests check more often)
    public SpotifyManager(SpotifyComponentModel model, SpotifyLoginStore? store = null, TimeSpan? checkInterval = null)
    {
        _model = model;
        _store = store ?? new SpotifyLoginStore();
        _lifetime = new CancellationTokenSource();
        _run = null;
        _session = null;

        Status = ConnectionStatus.NotConnected;
        StatusMessage = "Not connected";

        _model.ConnectionChanged += OnConnectionChanged;

        if (_model.IsEnabled) Start();

        _ = KeepLoginFreshAsync(checkInterval ?? DefaultCheckInterval, _lifetime.Token);
    }



    // playback



    // a song, or a playlist / album / artist from its start. If spotify isn't playing anywhere it starts on the computer
    // the spotify app is open on
    public Task<bool> TryPlayAsync(string what, SpotifyFind find, CancellationToken ct = default) =>
        TryChangeAsync($"play '{what}'", async (token, c) =>
        {
            var uri = await FindUriAsync(token, what, find, c);
            var body = uri.StartsWith("spotify:track:")
                ? new JsonObject { ["uris"] = new JsonArray(uri) }
                : new JsonObject { ["context_uri"] = uri };
            await SpotifyApi.PlayAsync(token, await DeviceAsync(token, c), body, c);
        }, ct);



    // a song after the one playing (spotify's queue)
    public Task<bool> TryQueueAsync(string song, CancellationToken ct = default) =>
        TryChangeAsync($"queue '{song}'", async (token, c) =>
        {
            var uri = await FindUriAsync(token, song, SpotifyFind.Song, c);
            if (!uri.StartsWith("spotify:track:")) throw new Exception("only songs can be queued");
            await SpotifyApi.QueueAsync(token, uri, await DeviceAsync(token, c), c);
        }, ct);



    // change is Pause, Resume, Toggle, Next or Previous. Pausing while paused (or resuming while playing) does nothing
    public Task<bool> TrySetPlaybackAsync(string change, CancellationToken ct = default) =>
        TryChangeAsync($"{change.ToLowerInvariant()} spotify", async (token, c) =>
        {
            if (change == "Next") { await SpotifyApi.NextAsync(token, c); return; }
            if (change == "Previous") { await SpotifyApi.PreviousAsync(token, c); return; }

            var player = await SpotifyApi.GetPlayerAsync(token, c);
            var playing = player?.GetProperty("is_playing").GetBoolean() ?? false;
            var play = change switch { "Pause" => false, "Resume" => true, _ => !playing };
            if (play == playing) return;

            if (play) await SpotifyApi.PlayAsync(token, player == null ? await DeviceAsync(token, c) : null, null, c);
            else await SpotifyApi.PauseAsync(token, c);
        }, ct);



    // 0 to 100, kept in that range
    public Task<bool> TrySetVolumeAsync(int percent, CancellationToken ct = default) =>
        TryChangeAsync("set spotify volume", (token, c) => SpotifyApi.SetVolumeAsync(token, Math.Clamp(percent, 0, 100), c), ct);



    // on: true / false, null toggles. Already that way does nothing
    public Task<bool> TrySetShuffleAsync(bool? on, CancellationToken ct = default) =>
        TryChangeAsync("set spotify shuffle", async (token, c) =>
        {
            var player = await SpotifyApi.GetPlayerAsync(token, c) ?? throw new Exception("nothing is playing on spotify");
            var current = player.GetProperty("shuffle_state").GetBoolean();
            var wanted = on ?? !current;
            if (wanted != current) await SpotifyApi.SetShuffleAsync(token, wanted, c);
        }, ct);



    // mode is Off, Song (repeat the song) or Playlist (repeat the playlist / album). Already that way does nothing
    public Task<bool> TrySetRepeatAsync(string mode, CancellationToken ct = default) =>
        TryChangeAsync("set spotify repeat", async (token, c) =>
        {
            var state = mode switch { "Song" => "track", "Playlist" => "context", _ => "off" };
            var player = await SpotifyApi.GetPlayerAsync(token, c) ?? throw new Exception("nothing is playing on spotify");
            if (player.GetProperty("repeat_state").GetString() != state) await SpotifyApi.SetRepeatAsync(token, state, c);
        }, ct);



    // what's playing (text, a whole number or true/false, see SpotifyInfos.Playback). Nothing playing anywhere reads as
    // not playing, empty text and 0
    public Task<(bool Ok, object? Value)> TryGetPlaybackAsync(SpotifyInfo info, CancellationToken ct = default) =>
        TryRequestAsync<object>($"read spotify {info.Name.ToLowerInvariant()}", async (token, c) =>
        {
            var player = await SpotifyApi.GetPlayerAsync(token, c);
            var song = player?.TryGetProperty("item", out var item) == true && item.ValueKind == JsonValueKind.Object ? item : (JsonElement?)null;

            return info.Name switch
            {
                "Playing" => player?.GetProperty("is_playing").GetBoolean() ?? false,
                "Progress Seconds" => player?.TryGetProperty("progress_ms", out var p) == true && p.ValueKind == JsonValueKind.Number ? p.GetInt32() / 1000 : 0,
                "Volume" => player?.GetProperty("device").TryGetProperty("volume_percent", out var v) == true && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0,
                "Shuffle" => player?.GetProperty("shuffle_state").GetBoolean() ?? false,
                "Repeat" => player?.GetProperty("repeat_state").GetString() switch { "track" => "Song", "context" => "Playlist", _ => "Off" },
                _ => SongInfo(song, info.Name),
            };
        }, ct);



    // songs and playlists



    // a song from a link or the first one a search finds (see SpotifyInfos.Song)
    public Task<(bool Ok, object? Value)> TryFindSongAsync(string song, SpotifyInfo info, CancellationToken ct = default) =>
        TryRequestAsync<object>($"find '{song}'", async (token, c) =>
        {
            var uri = await FindUriAsync(token, song, SpotifyFind.Song, c);
            if (!uri.StartsWith("spotify:track:")) throw new Exception("that's not a song link");
            return SongInfo(await SpotifyApi.GetTrackAsync(token, uri["spotify:track:".Length..], c), info.Name);
        }, ct);



    // a song added to the end of a playlist (one of yours by name, or a link). An empty song means the one playing now.
    // A song already in the playlist isn't added again
    public Task<bool> TryAddToPlaylistAsync(string playlist, string song, CancellationToken ct = default) =>
        TryChangeAsync($"add to playlist '{playlist}'", async (token, c) =>
        {
            var playlistUri = await FindUriAsync(token, playlist, SpotifyFind.Playlist, c);
            if (!playlistUri.StartsWith("spotify:playlist:")) throw new Exception("that's not a playlist link");
            var playlistId = playlistUri["spotify:playlist:".Length..];

            var songUri = song.Trim() == "" ? await PlayingSongUriAsync(token, c) : await FindUriAsync(token, song, SpotifyFind.Song, c);
            if (!songUri.StartsWith("spotify:track:")) throw new Exception("only songs can be added");

            if ((await SpotifyApi.GetPlaylistUrisAsync(token, playlistId, c)).Contains(songUri)) return;
            await SpotifyApi.AddToPlaylistAsync(token, playlistId, songUri, c);
        }, ct);



    // a link's spotify uri (spotify:track:id, ...), false for anything that isn't a spotify link
    public static bool TryParseLink(string text, out string uri)
    {
        var match = LinkPattern.Match(text.Trim());
        uri = match.Success ? $"spotify:{match.Groups["type"].Value.ToLowerInvariant()}:{match.Groups["id"].Value}" : "";
        return match.Success;
    }



    // model callbacks



    // the client id or enabled changed, or a reconnect was asked for
    private void OnConnectionChanged()
    {
        Stop();
        if (_model.IsEnabled) Start();
    }



    // connection



    private void Start()
    {
        _run = new CancellationTokenSource();
        _ = ConnectAsync(_model.ClientId, _run.Token);
    }



    private void Stop()
    {
        _run?.Cancel();
        _run?.Dispose();
        _run = null;

        _session = null;
        SetStatus(ConnectionStatus.NotConnected, "Not connected");
    }



    private async Task ConnectAsync(string clientId, CancellationToken ct)
    {
        SetStatus(ConnectionStatus.Connecting, "Connecting...");

        if (clientId == "")
        {
            SetStatus(ConnectionStatus.Failed, "Not connected");
            AppLog.WriteError("can't connect to spotify, no client id");
            return;
        }

        if (!_store.IsReadable)
        {
            SetStatus(ConnectionStatus.Failed, "Not connected");
            AppLog.WriteError("can't connect to spotify, auth.json can't be read");
            return;
        }

        try
        {
            // the saved login if this app made it, else log in through the browser
            SpotifyLoginData? session = null;
            var saved = _store.Load();
            if (saved.ClientId == clientId && saved.RefreshToken != "")
            {
                try
                {
                    session = await RefreshAsync(saved, ct);
                }
                catch (SpotifyUnauthorizedException)
                {
                    _store.Save(new SpotifyLoginData()); // spotify forgot it, so log in again
                }
            }

            session ??= await LogInAsync(clientId, ct);
            _store.Save(session);

            var me = await SpotifyApi.GetMeAsync(session.AccessToken, ct);
            var name = me.TryGetProperty("display_name", out var display) && display.GetString() is { Length: > 0 } shown ? shown : me.GetProperty("id").GetString();
            if (ct.IsCancellationRequested) return;

            _session = session;
            SetStatus(ConnectionStatus.Connected, $"Connected to {name}");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (ct.IsCancellationRequested) return;

            SetStatus(ConnectionStatus.Failed, "Not connected");
            AppLog.WriteError($"can't connect to spotify ({ex.Message})");
        }
    }



    // shows the login link and waits for the browser to come back with a code (Disconnect stops waiting)
    private async Task<SpotifyLoginData> LogInAsync(string clientId, CancellationToken ct)
    {
        var (verifier, challenge) = SpotifyApi.CreatePkce();
        var state = Guid.NewGuid().ToString("N");
        var callback = SpotifyLoginListener.WaitForCallbackAsync(SpotifyComponentModel.LoginPort, ct);

        SetStatus(ConnectionStatus.Connecting, "Waiting for login");
        AppLog.WritePrompt($"spotify login, open: {SpotifyApi.LoginUrl(clientId, SpotifyComponentModel.RedirectUri, challenge, state)}");

        var query = await callback;
        if (query.TryGetValue("error", out var error)) throw new Exception(error == "access_denied" ? "login cancelled" : error);
        if (!query.TryGetValue("state", out var returned) || returned != state || !query.TryGetValue("code", out var code)) throw new Exception("login link was out of date");

        var tokens = await SpotifyApi.ExchangeCodeAsync(clientId, SpotifyComponentModel.RedirectUri, code, verifier, ct);
        return ToLogin(clientId, tokens, "");
    }



    private static async Task<SpotifyLoginData> RefreshAsync(SpotifyLoginData login, CancellationToken ct) =>
        ToLogin(login.ClientId, await SpotifyApi.RefreshAsync(login.ClientId, login.RefreshToken, ct), login.RefreshToken);



    // spotify only sometimes sends a new refresh token, otherwise the old one keeps working
    private static SpotifyLoginData ToLogin(string clientId, JsonElement tokens, string oldRefreshToken) => new()
    {
        ClientId = clientId,
        AccessToken = tokens.GetProperty("access_token").GetString()!,
        RefreshToken = tokens.TryGetProperty("refresh_token", out var refresh) && refresh.GetString() is { Length: > 0 } fresh ? fresh : oldRefreshToken,
        ExpiresAt = DateTime.UtcNow.AddSeconds(tokens.GetProperty("expires_in").GetInt32()),
    };



    // checks the login every interval until the app closes, refreshing it when it has under RefreshBefore left
    private async Task KeepLoginFreshAsync(TimeSpan interval, CancellationToken ct)
    {
        while (true)
        {
            try
            {
                await Task.Delay(interval, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            var session = _session;
            if (session == null || session.ExpiresAt - DateTime.UtcNow > RefreshBefore) continue;

            try
            {
                var refreshed = await RefreshAsync(session, ct);
                // disconnected (or reconnected) meanwhile: that connection has its own login
                if (Interlocked.CompareExchange(ref _session, refreshed, session) == session) _store.Save(refreshed);
            }
            catch (SpotifyUnauthorizedException)
            {
                OnLoginLost(session);
            }
            catch (Exception)
            {
                // spotify couldn't be reached right now, the next check tries again (the token still has a while left)
            }
        }
    }



    // spotify refused the refresh token: forget it, say so once, Retry logs in again
    private void OnLoginLost(SpotifyLoginData session)
    {
        if (Interlocked.CompareExchange(ref _session, null, session) != session) return;

        _store.Save(new SpotifyLoginData());
        SetStatus(ConnectionStatus.Failed, "Login expired");
        AppLog.WriteWarning("spotify login expired, press retry");
    }



    // spotify refused a request's token: stop using it (the saved login is kept, so Retry uses it again without logging
    // in), say it disconnected once (later requests just say it isn't connected)
    private void OnLoginRefused(SpotifyLoginData session)
    {
        if (Interlocked.CompareExchange(ref _session, null, session) != session) return;

        SetStatus(ConnectionStatus.Failed, "Disconnected");
        AppLog.WriteWarning("spotify disconnected (login refused)");
    }



    // requests



    private async Task<bool> TryChangeAsync(string what, Func<string, CancellationToken, Task> change, CancellationToken ct) =>
        (await TryRequestAsync<bool>(what, async (token, c) => { await change(token, c); return true; }, ct)).Ok;



    // runs a request with the current token, logging why if it fails (Value is default then). No refresh or retry here
    // (only startup, Connect / Retry and the background check do that), so a refused login shows up
    private async Task<(bool Ok, T? Value)> TryRequestAsync<T>(string what, Func<string, CancellationToken, Task<T>> request, CancellationToken ct)
    {
        var session = _session;
        if (session == null)
        {
            AppLog.WriteError($"can't {what}, spotify not connected");
            return (false, default);
        }

        try
        {
            return (true, await request(session.AccessToken, ct));
        }
        catch (SpotifyUnauthorizedException)
        {
            OnLoginRefused(session);
            AppLog.WriteError($"can't {what} (login refused)");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            AppLog.WriteError($"can't {what} ({ex.Message})");
        }
        return (false, default);
    }



    // a link as it is, or words looked up: the first song / album a search finds, or one of your playlists by name
    private static async Task<string> FindUriAsync(string token, string text, SpotifyFind find, CancellationToken ct)
    {
        var words = text.Trim();
        if (words == "") throw new Exception("nothing given");
        if (TryParseLink(words, out var uri)) return uri;

        if (find == SpotifyFind.Playlist)
        {
            var playlist = (await SpotifyApi.GetMyPlaylistsAsync(token, ct))
                .FirstOrDefault(p => string.Equals(p.GetProperty("name").GetString()?.Trim(), words, StringComparison.OrdinalIgnoreCase));
            return playlist.ValueKind == JsonValueKind.Object ? playlist.GetProperty("uri").GetString()! : throw new Exception("you have no playlist by that name");
        }

        var found = await SpotifyApi.SearchFirstAsync(token, find == SpotifyFind.Album ? "album" : "track", words, ct);
        return found?.GetProperty("uri").GetString() ?? throw new Exception($"no {(find == SpotifyFind.Album ? "album" : "song")} found");
    }



    // spotify isn't playing anywhere: the computer the app is open on (else any device), null if something is playing
    private static async Task<string?> DeviceAsync(string token, CancellationToken ct)
    {
        var devices = (await SpotifyApi.GetDevicesAsync(token, ct))
            .Where(d => !(d.TryGetProperty("is_restricted", out var r) && r.ValueKind == JsonValueKind.True)).ToList();
        if (devices.Any(d => d.GetProperty("is_active").GetBoolean())) return null;

        var device = devices.OrderBy(d => d.GetProperty("type").GetString() == "Computer" ? 0 : 1).FirstOrDefault();
        return device.ValueKind == JsonValueKind.Object ? device.GetProperty("id").GetString() : throw new Exception("spotify isn't open");
    }



    private static async Task<string> PlayingSongUriAsync(string token, CancellationToken ct)
    {
        var player = await SpotifyApi.GetPlayerAsync(token, ct);
        return player?.TryGetProperty("item", out var item) == true && item.ValueKind == JsonValueKind.Object
            ? item.GetProperty("uri").GetString()! : throw new Exception("nothing is playing on spotify");
    }



    // Title, Artist, Album, Link or Length Seconds of a song (empty / 0 without one)
    private static object SongInfo(JsonElement? song, string name)
    {
        if (song is not { } s) return name == "Length Seconds" ? 0 : "";

        return name switch
        {
            "Title" => s.GetProperty("name").GetString() ?? "",
            "Artist" => string.Join(", ", s.GetProperty("artists").EnumerateArray().Select(a => a.GetProperty("name").GetString())),
            "Album" => s.TryGetProperty("album", out var album) ? album.GetProperty("name").GetString() ?? "" : "",
            "Link" => s.TryGetProperty("external_urls", out var urls) && urls.TryGetProperty("spotify", out var url) ? url.GetString() ?? "" : "",
            "Length Seconds" => s.GetProperty("duration_ms").GetInt32() / 1000,
            _ => "",
        };
    }



    // status



    // can be called from any thread, listeners always get it on the UI thread
    private void SetStatus(ConnectionStatus status, string message) => Dispatcher.UIThread.Post(() =>
    {
        if (status == Status && message == StatusMessage) return;

        Status = status;
        StatusMessage = message;
        StatusChanged?.Invoke();
    });



    // disposal



    public void Dispose()
    {
        _model.ConnectionChanged -= OnConnectionChanged;
        _lifetime.Cancel();
        _lifetime.Dispose();
        _run?.Cancel();
        _run?.Dispose();
        _run = null;
        _session = null;
    }
}
