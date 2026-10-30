using System.Collections.Generic;
using System.Linq;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Spotify.Model;



// one thing Get Spotify Playback / Find Spotify Song can read: its dropdown name and the kind of value it is (picking it
// picks the block's type, like Get Stream Info)
public sealed record SpotifyInfo(string Name, ValueKind Kind, string Description);



// what each of those blocks offers, in dropdown order (SpotifyManager reads them)
public static class SpotifyInfos
{
    // what's playing right now (nothing playing anywhere: Playing false, empty text, 0)
    public static readonly IReadOnlyList<SpotifyInfo> Playback =
    [
        new("Title", ValueKind.Text, "the song's title"),
        new("Artist", ValueKind.Text, "its artists, comma separated"),
        new("Album", ValueKind.Text, "its album"),
        new("Link", ValueKind.Text, "its open.spotify.com link"),
        new("Playing", ValueKind.Bool, "whether it's playing (false while paused)"),
        new("Progress Seconds", ValueKind.Int, "how far into the song"),
        new("Length Seconds", ValueKind.Int, "how long the song is"),
        new("Volume", ValueKind.Int, "spotify's volume, 0 to 100"),
        new("Shuffle", ValueKind.Bool, "whether shuffle is on"),
        new("Repeat", ValueKind.Text, "Off, Song or Playlist"),
    ];

    // a song found by a link or a search
    public static readonly IReadOnlyList<SpotifyInfo> Song =
    [
        new("Title", ValueKind.Text, "the song's title"),
        new("Artist", ValueKind.Text, "its artists, comma separated"),
        new("Album", ValueKind.Text, "its album"),
        new("Link", ValueKind.Text, "its open.spotify.com link"),
        new("Length Seconds", ValueKind.Int, "how long the song is"),
    ];



    public static SpotifyInfo? Find(IReadOnlyList<SpotifyInfo> list, string name) => list.FirstOrDefault(i => i.Name == name);



    public static List<string> Names(IReadOnlyList<SpotifyInfo> list) => list.Select(i => i.Name).ToList();
}
