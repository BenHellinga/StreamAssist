using System.Threading.Tasks;

namespace StreamAssist.Scripting;



/// <summary>
/// Spotify (the Connections tab). Songs, playlists and albums are a Spotify link (Share > Copy link) or words to
/// search for (the first one found). Changing playback needs Spotify Premium. A request that fails is logged and throws
/// a <see cref="StreamAssistException"/>.
/// </summary>
public interface ISpotify
{
    /// <summary>Whether Spotify is connected.</summary>
    bool IsConnected { get; }

    /// <summary>Plays a song, or a playlist or album from its start (<paramref name="kind"/> says what words search for; one of your playlists by name).</summary>
    Task PlayAsync(string what, SpotifyKind kind = SpotifyKind.Song);

    /// <summary>Adds a song to the queue, to play after the current one.</summary>
    Task QueueAsync(string song);

    /// <summary>Resumes (On), pauses (Off) or toggles playback. Already that way: nothing happens.</summary>
    Task SetPlayingAsync(Switch change);

    /// <summary>Skips to the next song.</summary>
    Task NextAsync();

    /// <summary>Goes back to the previous song.</summary>
    Task PreviousAsync();

    /// <summary>Sets the volume, 0 to 100 (kept in that range; some devices, e.g. phones, don't allow it).</summary>
    Task SetVolumeAsync(int percent);

    /// <summary>Turns shuffle on, off or toggles it.</summary>
    Task SetShuffleAsync(Switch change);

    /// <summary>Sets repeat: off, the song, or the playlist / album.</summary>
    Task SetRepeatAsync(SpotifyRepeat repeat);

    /// <summary>What's playing, null when nothing is playing anywhere.</summary>
    Task<SpotifyPlayback?> GetPlaybackAsync();

    /// <summary>Looks up a song (a link, or the first one a search finds).</summary>
    Task<SpotifySong> FindSongAsync(string song);

    /// <summary>Adds a song to the end of a playlist (one of yours by name, or its link); an empty song adds the one playing. Already in it: nothing happens.</summary>
    Task AddToPlaylistAsync(string playlist, string song = "");
}



/// <summary>What words search Spotify for.</summary>
public enum SpotifyKind { Song, Playlist, Album }



/// <summary>Spotify's repeat setting.</summary>
public enum SpotifyRepeat { Off, Song, Playlist }



/// <summary>A song: its title, artists (comma separated), album, open.spotify.com link and length.</summary>
public sealed record SpotifySong(string Title, string Artist, string Album, string Link, int LengthSeconds);



/// <summary>What Spotify is doing: the song (null between songs), playing or paused, how far in, the volume and settings.</summary>
public sealed record SpotifyPlayback(SpotifySong? Song, bool IsPlaying, int ProgressSeconds, int Volume, bool Shuffle, SpotifyRepeat Repeat);
