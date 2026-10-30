using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace StreamAssist.Components.Spotify.Background;



// spotify refused the token (401), or refused the refresh token (the login is gone)
public class SpotifyUnauthorizedException(string message) : Exception(message);



// the only place that talks HTTP to Spotify (logins and the Web API), no Spotify library is used. Playback is done
// through Spotify Connect: it controls whatever device is playing (e.g. the desktop app), which needs Spotify Premium
public static class SpotifyApi
{
    // the addresses (the tests point them at a pretend spotify)
    public static string AccountsUrl { get; set; } = "https://accounts.spotify.com";
    public static string ApiUrl { get; set; } = "https://api.spotify.com/v1";

    // playback (read and change), and reading / adding to your playlists
    public static readonly string[] Scopes =
    [
        "user-read-playback-state",
        "user-modify-playback-state",
        "user-read-currently-playing",
        "playlist-read-private",
        "playlist-read-collaborative",
        "playlist-modify-public",
        "playlist-modify-private",
    ];

    private static readonly HttpClient Http = new();



    // login (authorization code with PKCE: the app has no secret, the browser comes back to RedirectUri with a code)



    // a random verifier and its challenge (sha256, base64url)
    public static (string Verifier, string Challenge) CreatePkce()
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(48));
        return (verifier, Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))));
    }



    public static string LoginUrl(string clientId, string redirectUri, string challenge, string state) =>
        $"{AccountsUrl}/authorize?client_id={E(clientId)}&response_type=code&redirect_uri={E(redirectUri)}" +
        $"&code_challenge_method=S256&code_challenge={E(challenge)}&state={E(state)}&scope={E(string.Join(' ', Scopes))}";



    // the code from the redirect, swapped for tokens (access_token, refresh_token, expires_in)
    public static Task<JsonElement> ExchangeCodeAsync(string clientId, string redirectUri, string code, string verifier, CancellationToken ct) =>
        TokenAsync(new()
        {
            ["client_id"] = clientId,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = verifier,
        }, ct);



    // a new access token (and maybe a new refresh token), SpotifyUnauthorizedException if the login is gone
    public static Task<JsonElement> RefreshAsync(string clientId, string refreshToken, CancellationToken ct) =>
        TokenAsync(new()
        {
            ["client_id"] = clientId,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
        }, ct);



    // you



    public static async Task<JsonElement> GetMeAsync(string token, CancellationToken ct) =>
        (await RequestAsync(token, HttpMethod.Get, "me", null, ct))!.Value;



    // playback



    // what's playing and where, null if nothing is playing anywhere
    public static Task<JsonElement?> GetPlayerAsync(string token, CancellationToken ct) =>
        RequestAsync(token, HttpMethod.Get, "me/player", null, ct);



    public static async Task<List<JsonElement>> GetDevicesAsync(string token, CancellationToken ct)
    {
        var result = await RequestAsync(token, HttpMethod.Get, "me/player/devices", null, ct);
        return result?.GetProperty("devices").EnumerateArray().ToList() ?? [];
    }



    // body: {"uris": [...]} for songs, {"context_uri": ...} for a playlist/album/artist, null to resume
    // deviceId null plays where spotify is already playing
    public static async Task PlayAsync(string token, string? deviceId, JsonObject? body, CancellationToken ct) =>
        await RequestAsync(token, HttpMethod.Put, $"me/player/play{Device(deviceId, '?')}", body, ct);



    public static async Task PauseAsync(string token, CancellationToken ct) =>
        await RequestAsync(token, HttpMethod.Put, "me/player/pause", null, ct);



    public static async Task NextAsync(string token, CancellationToken ct) =>
        await RequestAsync(token, HttpMethod.Post, "me/player/next", null, ct);



    public static async Task PreviousAsync(string token, CancellationToken ct) =>
        await RequestAsync(token, HttpMethod.Post, "me/player/previous", null, ct);



    public static async Task QueueAsync(string token, string uri, string? deviceId, CancellationToken ct) =>
        await RequestAsync(token, HttpMethod.Post, $"me/player/queue?uri={E(uri)}{Device(deviceId, '&')}", null, ct);



    public static async Task SetVolumeAsync(string token, int percent, CancellationToken ct) =>
        await RequestAsync(token, HttpMethod.Put, $"me/player/volume?volume_percent={percent}", null, ct);



    public static async Task SetShuffleAsync(string token, bool on, CancellationToken ct) =>
        await RequestAsync(token, HttpMethod.Put, $"me/player/shuffle?state={(on ? "true" : "false")}", null, ct);



    // state: off, track or context
    public static async Task SetRepeatAsync(string token, string state, CancellationToken ct) =>
        await RequestAsync(token, HttpMethod.Put, $"me/player/repeat?state={state}", null, ct);



    // songs and searching



    public static async Task<JsonElement> GetTrackAsync(string token, string id, CancellationToken ct) =>
        (await RequestAsync(token, HttpMethod.Get, $"tracks/{E(id)}", null, ct))!.Value;



    // the first result of a search, type is track or album, null if nothing was found
    public static async Task<JsonElement?> SearchFirstAsync(string token, string type, string query, CancellationToken ct)
    {
        var result = await RequestAsync(token, HttpMethod.Get, $"search?q={E(query)}&type={type}&limit=1", null, ct);
        var items = result!.Value.GetProperty($"{type}s").GetProperty("items");
        // spotify sometimes leaves null holes in search results
        return items.EnumerateArray().Where(i => i.ValueKind == JsonValueKind.Object).Select(i => (JsonElement?)i).FirstOrDefault();
    }



    // playlists



    // every playlist in your library (yours and the ones you follow)
    public static async Task<List<JsonElement>> GetMyPlaylistsAsync(string token, CancellationToken ct)
    {
        var playlists = new List<JsonElement>();
        for (int offset = 0; ; offset += 50)
        {
            var page = (await RequestAsync(token, HttpMethod.Get, $"me/playlists?limit=50&offset={offset}", null, ct))!.Value;
            playlists.AddRange(page.GetProperty("items").EnumerateArray().Where(p => p.ValueKind == JsonValueKind.Object));
            if (page.GetProperty("next").ValueKind != JsonValueKind.String) return playlists;
        }
    }



    // the uri of every song in a playlist
    public static async Task<List<string>> GetPlaylistUrisAsync(string token, string playlistId, CancellationToken ct)
    {
        var uris = new List<string>();
        for (int offset = 0; ; offset += 50)
        {
            var page = (await RequestAsync(token, HttpMethod.Get, $"playlists/{E(playlistId)}/items?limit=50&offset={offset}", null, ct))!.Value;
            foreach (var row in page.GetProperty("items").EnumerateArray())
            {
                // each row's song is "item" (spotify's older answer called it "track")
                var song = row.TryGetProperty("item", out var item) && item.ValueKind == JsonValueKind.Object ? item
                    : row.TryGetProperty("track", out var track) && track.ValueKind == JsonValueKind.Object ? track : default;
                if (song.ValueKind == JsonValueKind.Object && song.TryGetProperty("uri", out var uri) && uri.GetString() is { } text) uris.Add(text);
            }
            if (page.GetProperty("next").ValueKind != JsonValueKind.String) return uris;
        }
    }



    public static async Task AddToPlaylistAsync(string token, string playlistId, string uri, CancellationToken ct) =>
        await RequestAsync(token, HttpMethod.Post, $"playlists/{E(playlistId)}/items", new JsonObject { ["uris"] = new JsonArray(uri) }, ct);



    // helpers



    // the answer's json, null when spotify answers with nothing (204, e.g. nothing is playing)
    private static async Task<JsonElement?> RequestAsync(string token, HttpMethod method, string path, JsonNode? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, $"{ApiUrl}/{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        // spotify wants a body (even empty) on PUT / POST
        if (body != null || method != HttpMethod.Get) request.Content = new StringContent(body?.ToJsonString() ?? "", Encoding.UTF8, "application/json");

        using var response = await Http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized) throw new SpotifyUnauthorizedException(ReadMessage(text));
        if (!response.IsSuccessStatusCode) throw new Exception(ReadMessage(text, (int)response.StatusCode));

        if (string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            return JsonDocument.Parse(text).RootElement.Clone();
        }
        catch (JsonException)
        {
            return null; // some changes answer with a snapshot id as plain text
        }
    }



    private static async Task<JsonElement> TokenAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        using var response = await Http.PostAsync($"{AccountsUrl}/api/token", new FormUrlEncodedContent(form), ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        // spotify refused the code or refresh token (the login is gone), anything else is worth trying again later
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new SpotifyUnauthorizedException(ReadMessage(body));
        if (!response.IsSuccessStatusCode) throw new Exception(ReadMessage(body, (int)response.StatusCode));

        return JsonDocument.Parse(body).RootElement.Clone();
    }



    // spotify's errors are {"error": {"status", "message", "reason"}} from the api, {"error", "error_description"} from logins
    private static string ReadMessage(string body, int status = 0)
    {
        try
        {
            var error = JsonDocument.Parse(body).RootElement.GetProperty("error");
            if (error.ValueKind == JsonValueKind.String)
                return error.GetString() + (body.Contains("error_description") ? $": {JsonDocument.Parse(body).RootElement.GetProperty("error_description").GetString()}" : "");

            var reason = error.TryGetProperty("reason", out var r) ? r.GetString() : null;
            return reason switch
            {
                "PREMIUM_REQUIRED" => "needs spotify premium",
                "NO_ACTIVE_DEVICE" => "nothing is playing on spotify",
                _ => error.TryGetProperty("message", out var m) ? m.GetString() ?? $"{status}" : $"{status}",
            };
        }
        catch
        {
            return status == 0 ? "no answer" : $"{status} {body}".Trim();
        }
    }



    private static string Device(string? deviceId, char separator) => deviceId == null ? "" : $"{separator}device_id={E(deviceId)}";



    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');



    private static string E(string value) => Uri.EscapeDataString(value);
}
