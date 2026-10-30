using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StreamAssist.Components.Spotify.Background;

namespace StreamAssist.Tests;



// a pretend Spotify for the tests: the token endpoint and the parts of the Web API SpotifyManager uses, on a free
// local port, with one computer device, a search that knows two songs, and one playlist. Every api request is recorded
public sealed class FakeSpotifyServer : IDisposable
{
    public const string GoodRefreshToken = "good-refresh";

    private readonly HttpListener _listener;
    private readonly string _oldAccounts;
    private readonly string _oldApi;
    private int _tokens;

    public string Token = ""; // the access token it accepts right now
    public bool RefuseRefresh; // the refresh token was revoked
    public int ExpiresIn = 3600; // seconds a new access token lasts
    public int Refreshes; // refresh token grants so far
    public bool Active; // something is playing somewhere (else /me/player answers 204)
    public bool Playing;
    public bool Shuffle;
    public string Repeat = "off";
    public int Volume = 40;
    public JsonObject? Song; // what's playing
    public readonly List<string> Requests = []; // "PUT me/player/play?device_id=pc {...}"
    public readonly List<string> Queue = [];
    public readonly List<string> Playlist = ["spotify:track:never"];

    public static JsonObject Track(string id, string name, string artist, int seconds) => new()
    {
        ["uri"] = $"spotify:track:{id}",
        ["id"] = id,
        ["name"] = name,
        ["artists"] = new JsonArray(new JsonObject { ["name"] = artist }, new JsonObject { ["name"] = "Someone" }),
        ["album"] = new JsonObject { ["name"] = $"{name} (Album)" },
        ["external_urls"] = new JsonObject { ["spotify"] = $"https://open.spotify.com/track/{id}" },
        ["duration_ms"] = seconds * 1000,
    };



    public FakeSpotifyServer()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Start();
        _ = ServeAsync();

        _oldAccounts = SpotifyApi.AccountsUrl;
        _oldApi = SpotifyApi.ApiUrl;
        SpotifyApi.AccountsUrl = $"http://localhost:{port}";
        SpotifyApi.ApiUrl = $"http://localhost:{port}/v1";
    }



    private async Task ServeAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try { context = await _listener.GetContextAsync(); }
            catch { return; }

            try { Handle(context); }
            catch (Exception ex) { Reply(context, 500, new JsonObject { ["error"] = new JsonObject { ["status"] = 500, ["message"] = ex.Message } }); }
        }
    }



    private void Handle(HttpListenerContext context)
    {
        var request = context.Request;
        var path = request.Url!.AbsolutePath.TrimStart('/');
        var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
        var body = new StreamReader(request.InputStream).ReadToEnd();

        if (path == "api/token")
        {
            var form = System.Web.HttpUtility.ParseQueryString(body);
            var good = form["grant_type"] == "authorization_code" ? form["code"] == "the-code" && form["code_verifier"] is { Length: > 40 }
                : form["refresh_token"] == GoodRefreshToken && !RefuseRefresh;
            if (!good) { Reply(context, 400, new JsonObject { ["error"] = "invalid_grant", ["error_description"] = "Refresh token revoked" }); return; }

            Token = $"token-{++_tokens}";
            if (form["grant_type"] == "refresh_token") Interlocked.Increment(ref Refreshes);
            var tokens = new JsonObject { ["access_token"] = Token, ["expires_in"] = ExpiresIn };
            if (form["grant_type"] == "authorization_code") tokens["refresh_token"] = GoodRefreshToken;
            Reply(context, 200, tokens);
            return;
        }

        path = path["v1/".Length..];
        lock (Requests) Requests.Add($"{request.HttpMethod} {path}{request.Url.Query} {body}".Trim());

        if (request.Headers["Authorization"] != $"Bearer {Token}")
        {
            Reply(context, 401, new JsonObject { ["error"] = new JsonObject { ["status"] = 401, ["message"] = "The access token expired" } });
            return;
        }

        JsonNode? answer = null;
        switch (request.HttpMethod, path)
        {
            case ("GET", "me"):
                answer = new JsonObject { ["id"] = "budder", ["display_name"] = "Budder" };
                break;
            case ("GET", "me/player"):
                if (!Active) break;
                answer = new JsonObject
                {
                    ["is_playing"] = Playing, ["progress_ms"] = 61500, ["shuffle_state"] = Shuffle, ["repeat_state"] = Repeat,
                    ["device"] = new JsonObject { ["volume_percent"] = Volume }, ["item"] = Song?.DeepClone(),
                };
                break;
            case ("GET", "me/player/devices"):
                answer = new JsonObject { ["devices"] = new JsonArray(
                    new JsonObject { ["id"] = "phone", ["type"] = "Smartphone", ["is_active"] = false },
                    new JsonObject { ["id"] = "pc", ["type"] = "Computer", ["is_active"] = Active }) };
                break;
            case ("PUT", "me/player/play"):
                var play = string.IsNullOrEmpty(body) ? null : JsonNode.Parse(body);
                if (play?["uris"] is JsonArray uris) Song = Find(uris[0]!.GetValue<string>()["spotify:track:".Length..]);
                Active = Playing = true;
                break;
            case ("PUT", "me/player/pause"):
                if (!Active) { Reply(context, 404, new JsonObject { ["error"] = new JsonObject { ["status"] = 404, ["message"] = "Player command failed: No active device found", ["reason"] = "NO_ACTIVE_DEVICE" } }); return; }
                Playing = false;
                break;
            case ("POST", "me/player/next" or "me/player/previous"):
                break;
            case ("POST", "me/player/queue"):
                Queue.Add(query["uri"]!);
                break;
            case ("PUT", "me/player/volume"):
                Volume = int.Parse(query["volume_percent"]!);
                break;
            case ("PUT", "me/player/shuffle"):
                Shuffle = query["state"] == "true";
                break;
            case ("PUT", "me/player/repeat"):
                Repeat = query["state"]!;
                break;
            case ("GET", "search"):
                var q = query["q"]!.ToLowerInvariant();
                var found = q.Contains("rick") ? Find("never") : q.Contains("sandstorm") ? Find("sand") : null;
                answer = new JsonObject { [$"{query["type"]}s"] = new JsonObject { ["items"] = found == null ? new JsonArray() : new JsonArray(found) } };
                break;
            case ("GET", "me/playlists"):
                answer = new JsonObject { ["items"] = new JsonArray(
                    new JsonObject { ["id"] = "likes", ["name"] = "Stream Likes", ["uri"] = "spotify:playlist:likes" }), ["next"] = null };
                break;
            case ("GET", "playlists/likes/items"):
                answer = new JsonObject { ["items"] = new JsonArray(Playlist.Select(u => (JsonNode)new JsonObject { ["item"] = new JsonObject { ["uri"] = u } }).ToArray()), ["next"] = null };
                break;
            case ("POST", "playlists/likes/items"):
                Playlist.AddRange(JsonNode.Parse(body)!["uris"]!.AsArray().Select(u => u!.GetValue<string>()));
                answer = new JsonObject { ["snapshot_id"] = "x" };
                break;
            default:
                if (request.HttpMethod == "GET" && path.StartsWith("tracks/") && Find(path["tracks/".Length..]) is { } track) { answer = track; break; }
                Reply(context, 404, new JsonObject { ["error"] = new JsonObject { ["status"] = 404, ["message"] = "Resource not found" } });
                return;
        }

        Reply(context, answer == null ? 204 : 200, answer);
    }



    private static JsonObject? Find(string id) => id switch
    {
        "never" => Track("never", "Never Gonna Give You Up", "Rick Astley", 213),
        "sand" => Track("sand", "Sandstorm", "Darude", 225),
        _ => null,
    };



    private static void Reply(HttpListenerContext context, int status, JsonNode? body)
    {
        context.Response.StatusCode = status;
        if (body != null)
        {
            var bytes = Encoding.UTF8.GetBytes(body.ToJsonString());
            context.Response.ContentType = "application/json";
            context.Response.OutputStream.Write(bytes);
        }
        context.Response.Close();
    }



    public void Dispose()
    {
        SpotifyApi.AccountsUrl = _oldAccounts;
        SpotifyApi.ApiUrl = _oldApi;
        _listener.Close();
    }
}



// keeps the login in memory instead of Config/auth.json
public sealed class MemorySpotifyLoginStore : SpotifyLoginStore
{
    public SpotifyLoginData Login = new();

    public override SpotifyLoginData Load() => Login;

    public override void Save(SpotifyLoginData login) => Login = login;
}
