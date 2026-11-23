using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Spotify.Background;
using StreamAssist.Core.Connections;
using StreamAssist.Scripting;
using StreamAssist.Shell;
using StreamAssist.Core.Utilities;

namespace StreamAssist.Components.CSharp.Api;



// ISpotify: the spotify manager does each request and logs its own failures (so those throw already logged)
public sealed class SpotifyScriptApi(TriggerContext context) : ScriptApiPart(context), ISpotify
{
    private static SpotifyManager Spotify => Need(GlobalContext.SpotifyManager, "spotify");



    public bool IsConnected => UiThread.Run<bool>(() => GlobalContext.SpotifyManager?.Status == ConnectionStatus.Connected);



    public Task PlayAsync(string what, SpotifyKind kind = SpotifyKind.Song) => UiThread.RunAsync(async () =>
    {
        var spotify = Spotify;
        if (what.Trim() == "") throw Error("nothing to play");
        var find = kind switch { SpotifyKind.Playlist => SpotifyFind.Playlist, SpotifyKind.Album => SpotifyFind.Album, _ => SpotifyFind.Song };
        if (!await spotify.TryPlayAsync(what, find, Token)) throw Logged("not played");
    });



    public Task QueueAsync(string song) => UiThread.RunAsync(async () =>
    {
        var spotify = Spotify;
        if (song.Trim() == "") throw Error("no song given");
        if (!await spotify.TryQueueAsync(song, Token)) throw Logged("not queued");
    });



    public Task SetPlayingAsync(Switch change) => UiThread.RunAsync(() => SetPlaybackAsync(change switch { Switch.On => "Resume", Switch.Off => "Pause", _ => "Toggle" }));

    public Task NextAsync() => UiThread.RunAsync(() => SetPlaybackAsync("Next"));

    public Task PreviousAsync() => UiThread.RunAsync(() => SetPlaybackAsync("Previous"));



    // for blocks: Pause, Resume, Toggle, Next or Previous
    public Task SetPlaybackAsync(string change) => UiThread.RunAsync(async () =>
    {
        if (!await Spotify.TrySetPlaybackAsync(change, Token)) throw Logged("playback not changed");
    });



    public Task SetVolumeAsync(int percent) => UiThread.RunAsync(async () =>
    {
        if (!await Spotify.TrySetVolumeAsync(percent, Token)) throw Logged("volume not set");
    });



    public Task SetShuffleAsync(Switch change) => UiThread.RunAsync(async () =>
    {
        bool? on = change switch { Switch.On => true, Switch.Off => false, _ => null };
        if (!await Spotify.TrySetShuffleAsync(on, Token)) throw Logged("shuffle not set");
    });



    public Task SetRepeatAsync(SpotifyRepeat repeat) => UiThread.RunAsync(async () =>
    {
        if (!await Spotify.TrySetRepeatAsync(repeat.ToString(), Token)) throw Logged("repeat not set");
    });



    public Task<SpotifyPlayback?> GetPlaybackAsync() => UiThread.RunAsync<SpotifyPlayback?>(async () =>
    {
        var (ok, playback) = await Spotify.TryGetPlaybackAsync(Token);
        return ok ? playback : throw Logged("playback not read");
    });



    public Task<SpotifySong> FindSongAsync(string song) => UiThread.RunAsync<SpotifySong>(async () =>
    {
        var spotify = Spotify;
        if (song.Trim() == "") throw Error("no song given");
        var (ok, found) = await spotify.TryFindSongAsync(song, Token);
        return ok ? found! : throw Logged("song not found");
    });



    public Task AddToPlaylistAsync(string playlist, string song = "") => UiThread.RunAsync(async () =>
    {
        var spotify = Spotify;
        if (playlist.Trim() == "") throw Error("no playlist given");
        if (!await spotify.TryAddToPlaylistAsync(playlist, song, Token)) throw Logged("not added");
    });
}
