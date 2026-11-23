using System.Collections.Generic;
using System.Threading.Tasks;

namespace StreamAssist.Scripting;



/// <summary>
/// Sounds and text to speech, played by the app (its own audio, not OBS's). With no dock they play straight away, over
/// anything else playing; naming a sound dock on the Dashboard queues them there, one at a time, where they can be
/// skipped or stopped. Volume is 0 to 1.
/// </summary>
public interface ISound
{
    /// <summary>
    /// Plays an .mp3 or .wav file inside Assets (e.g. "Sounds/airhorn.mp3"); a folder plays one of its sounds at random.
    /// <paramref name="waitUntilDone"/> waits until it has finished (or was stopped).
    /// </summary>
    Task PlayAsync(string file, double volume = 1, string? dock = null, string? device = null, bool waitUntilDone = false);

    /// <summary>
    /// Reads text out loud with a Windows voice or a Kokoro voice (null: the default). Speed is -10 (slowest) to 10.
    /// <paramref name="waitUntilDone"/> waits until it has finished speaking (or was stopped).
    /// </summary>
    Task SpeakAsync(string text, string? voice = null, int speed = 0, double volume = 1, string? dock = null, string? device = null, bool waitUntilDone = false);

    /// <summary>Every voice <see cref="SpeakAsync"/> can use.</summary>
    IReadOnlyList<string> Voices { get; }

    /// <summary>Every audio output device by name (null or "Default" plays on the default one).</summary>
    IReadOnlyList<string> OutputDevices { get; }
}
