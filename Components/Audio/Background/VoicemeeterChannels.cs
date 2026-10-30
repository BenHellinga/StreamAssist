using System.Collections.Generic;
using System.Linq;

namespace StreamAssist.Components.Audio.Background;



// one channel as Voicemeeter's window names it (an input strip like "Stereo Input 1", or an output bus like "B2"), and
// its name in Voicemeeter's remote API ("Strip[0]", "Bus[4]"), which parameters hang off ("Strip[0].Mute")
public sealed record VoicemeeterChannel(string Name, string Prefix, bool IsInput);



// the channels each Voicemeeter edition has, in the order its window shows them: hardware inputs, virtual inputs,
// then the A (hardware) and B (virtual) output buses. Basic = 2 + 1 inputs, A1 B1; Banana = 3 + 2, A1-A3 B1-B2;
// Potato = 5 + 3, A1-A5 B1-B3
public static class VoicemeeterChannels
{
    public const int Basic = 1;
    public const int Banana = 2;
    public const int Potato = 3;

    private static readonly string[] VirtualInputs = ["Voicemeeter Input", "Voicemeeter AUX", "Voicemeeter VAIO3"];



    public static IReadOnlyList<VoicemeeterChannel> For(int edition)
    {
        var (hardware, virtualInputs, aBuses, bBuses) = edition switch
        {
            Basic => (2, 1, 1, 1),
            Potato => (5, 3, 5, 3),
            _ => (3, 2, 3, 2),
        };

        var channels = new List<VoicemeeterChannel>();
        for (int i = 0; i < hardware; i++) channels.Add(new($"Stereo Input {i + 1}", $"Strip[{i}]", true));
        for (int i = 0; i < virtualInputs; i++) channels.Add(new(VirtualInputs[i], $"Strip[{hardware + i}]", true));
        for (int i = 0; i < aBuses; i++) channels.Add(new($"A{i + 1}", $"Bus[{i}]", false));
        for (int i = 0; i < bBuses; i++) channels.Add(new($"B{i + 1}", $"Bus[{aBuses + i}]", false));
        return channels;
    }



    public static List<string> Names(IReadOnlyList<VoicemeeterChannel> channels, bool? inputs = null) =>
        channels.Where(c => inputs == null || c.IsInput == inputs).Select(c => c.Name).ToList();
}
