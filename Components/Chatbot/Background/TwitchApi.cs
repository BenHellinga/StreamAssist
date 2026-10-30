using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace StreamAssist.Components.Chatbot.Background;



// thrown when twitch says the token isn't valid any more (expired or revoked)
public class TwitchUnauthorizedException(string message) : Exception(message);



// thin wrapper around the twitch http endpoints the chatbot uses
// (id.twitch.tv for logging in, api.twitch.tv/helix for everything else)
public static class TwitchApi
{
    private const string AuthUrl = "https://id.twitch.tv/oauth2";
    private const string HelixUrl = "https://api.twitch.tv/helix";

    private static readonly HttpClient Http = new();



    // device code login: step 1, ask twitch for a code the user types in at twitch.tv/activate
    public static async Task<JsonElement> StartDeviceLoginAsync(string clientId, IEnumerable<string> scopes, CancellationToken ct)
    {
        return await PostFormAsync($"{AuthUrl}/device", new()
        {
            ["client_id"] = clientId,
            ["scopes"] = string.Join(' ', scopes),
        }, ct) ?? throw new Exception("Twitch didn't return a login code (is the client id right?)");
    }



    // device code login: step 2, returns the token once the user has entered the code, null while still waiting
    public static async Task<JsonElement?> PollDeviceLoginAsync(string clientId, string clientSecret, string deviceCode, IEnumerable<string> scopes, CancellationToken ct)
    {
        using var response = await Http.PostAsync($"{AuthUrl}/token", new FormUrlEncodedContent(WithSecret(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["scopes"] = string.Join(' ', scopes),
            ["device_code"] = deviceCode,
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
        }, clientSecret)), ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        if (response.IsSuccessStatusCode) return JsonDocument.Parse(body).RootElement.Clone();
        if (body.Contains("authorization_pending")) return null;

        throw new Exception($"Twitch login failed: {ReadMessage(body)}");
    }



    // swaps a refresh token for a new access token (confidential apps also send their secret)
    public static async Task<JsonElement> RefreshAsync(string clientId, string clientSecret, string refreshToken, CancellationToken ct)
    {
        using var response = await Http.PostAsync($"{AuthUrl}/token", new FormUrlEncodedContent(WithSecret(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token",
        }, clientSecret)), ct);

        var body = await response.Content.ReadAsStringAsync(ct);

        // twitch refused the refresh token (the login is gone), anything else (e.g. twitch being down) is worth trying again later
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new TwitchUnauthorizedException($"Couldn't refresh the Twitch login: {ReadMessage(body)}");
        if (!response.IsSuccessStatusCode) throw new Exception($"Couldn't refresh the Twitch login: {(int)response.StatusCode} {ReadMessage(body)}");

        return JsonDocument.Parse(body).RootElement.Clone();
    }



    // checks a token, returns its login/user_id/scopes, or null if it's no longer valid
    public static async Task<JsonElement?> ValidateAsync(string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{AuthUrl}/validate");
        request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);

        using var response = await Http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized) return null;

        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw new Exception($"Couldn't check the Twitch login: {ReadMessage(body)}");

        return JsonDocument.Parse(body).RootElement.Clone();
    }



    // looks up a channel by its login name, returns (id, display name) or null if it doesn't exist
    public static async Task<(string Id, string DisplayName)?> GetUserAsync(TwitchSession session, string login, CancellationToken ct)
    {
        var result = await HelixAsync(session, HttpMethod.Get, $"users?login={Uri.EscapeDataString(login)}", null, ct);
        var data = result.GetProperty("data");
        if (data.GetArrayLength() == 0) return null;

        var user = data[0];
        return (user.GetProperty("id").GetString()!, user.GetProperty("display_name").GetString()!);
    }



    // asks twitch to send one kind of event over our websocket session
    public static async Task SubscribeAsync(TwitchSession session, string sessionId, string type, string version, Dictionary<string, string> condition, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["type"] = type,
            ["version"] = version,
            ["condition"] = JsonSerializer.SerializeToNode(condition),
            ["transport"] = new JsonObject { ["method"] = "websocket", ["session_id"] = sessionId },
        };

        await HelixAsync(session, HttpMethod.Post, "eventsub/subscriptions", body, ct);
    }



    // sends a chat message as the logged in account, throws if twitch accepted it but didn't post it (e.g. automod)
    public static async Task SendChatMessageAsync(TwitchSession session, string broadcasterId, string message, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["broadcaster_id"] = broadcasterId,
            ["sender_id"] = session.UserId,
            ["message"] = message,
        };

        var result = await HelixAsync(session, HttpMethod.Post, "chat/messages", body, ct);

        if (result.ValueKind == JsonValueKind.Object && result.TryGetProperty("data", out var data) && data.GetArrayLength() > 0
            && data[0].TryGetProperty("is_sent", out var isSent) && !isSent.GetBoolean())
        {
            var reason = data[0].TryGetProperty("drop_reason", out var drop) && drop.ValueKind == JsonValueKind.Object
                && drop.TryGetProperty("message", out var dropMessage) ? dropMessage.GetString() : null;
            throw new Exception($"twitch didn't post it ({reason ?? "no reason given"})");
        }
    }



    // bans a user, or times them out for this many seconds, as the logged in account (it has to be a moderator)
    public static async Task BanUserAsync(TwitchSession session, string broadcasterId, string userId, int? timeoutSeconds, string reason, CancellationToken ct)
    {
        var data = new JsonObject { ["user_id"] = userId };
        if (timeoutSeconds is { } seconds) data["duration"] = seconds;
        if (!string.IsNullOrWhiteSpace(reason)) data["reason"] = reason;

        var path = $"moderation/bans?broadcaster_id={Uri.EscapeDataString(broadcasterId)}&moderator_id={Uri.EscapeDataString(session.UserId)}";
        await HelixAsync(session, HttpMethod.Post, path, new JsonObject { ["data"] = data }, ct);
    }



    // deletes one chat message by its id, as the logged in account (it has to be a moderator)
    public static async Task DeleteChatMessageAsync(TwitchSession session, string broadcasterId, string messageId, CancellationToken ct)
    {
        var path = $"moderation/chat?broadcaster_id={Uri.EscapeDataString(broadcasterId)}&moderator_id={Uri.EscapeDataString(session.UserId)}"
            + $"&message_id={Uri.EscapeDataString(messageId)}";
        await HelixAsync(session, HttpMethod.Delete, path, null, ct);
    }



    // every channel point reward on the channel (as the broadcaster), each one twitch's reward json
    public static async Task<List<JsonElement>> GetCustomRewardsAsync(TwitchSession session, string broadcasterId, CancellationToken ct)
    {
        var result = await HelixAsync(session, HttpMethod.Get, $"channel_points/custom_rewards?broadcaster_id={Uri.EscapeDataString(broadcasterId)}", null, ct);
        return result.GetProperty("data").EnumerateArray().ToList();
    }



    // changes some of a reward's settings (only rewards this app created)
    public static async Task UpdateCustomRewardAsync(TwitchSession session, string broadcasterId, string rewardId, JsonObject changes, CancellationToken ct)
    {
        var path = $"channel_points/custom_rewards?broadcaster_id={Uri.EscapeDataString(broadcasterId)}&id={Uri.EscapeDataString(rewardId)}";
        await HelixAsync(session, HttpMethod.Patch, path, changes, ct);
    }



    // creates a reward owned by this app (so the app can change it later)
    public static async Task CreateCustomRewardAsync(TwitchSession session, string broadcasterId, JsonObject reward, CancellationToken ct)
    {
        var path = $"channel_points/custom_rewards?broadcaster_id={Uri.EscapeDataString(broadcasterId)}";
        await HelixAsync(session, HttpMethod.Post, path, reward, ct);
    }



    // helpers



    private static async Task<JsonElement> HelixAsync(TwitchSession session, HttpMethod method, string path, JsonNode? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, $"{HelixUrl}/{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        request.Headers.Add("Client-Id", session.ClientId);
        if (body != null) request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        using var response = await Http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized) throw new TwitchUnauthorizedException(ReadMessage(text));
        if (!response.IsSuccessStatusCode) throw new Exception($"{(int)response.StatusCode} {ReadMessage(text)}");

        return string.IsNullOrWhiteSpace(text) ? default : JsonDocument.Parse(text).RootElement.Clone();
    }



    private static async Task<JsonElement?> PostFormAsync(string url, Dictionary<string, string> form, CancellationToken ct)
    {
        using var response = await Http.PostAsync(url, new FormUrlEncodedContent(form), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw new Exception($"Twitch said: {ReadMessage(body)}");

        return JsonDocument.Parse(body).RootElement.Clone();
    }



    // confidential apps (the only kind twitch lets you register now) must include their secret
    private static Dictionary<string, string> WithSecret(Dictionary<string, string> form, string clientSecret)
    {
        if (!string.IsNullOrWhiteSpace(clientSecret)) form["client_secret"] = clientSecret;
        return form;
    }



    // twitch errors look like {"status":400,"message":"..."}
    private static string ReadMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var message)) return message.GetString() ?? body;
        }
        catch (JsonException) { }

        return body;
    }
}
