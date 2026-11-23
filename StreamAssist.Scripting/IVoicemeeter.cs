namespace StreamAssist.Scripting;



/// <summary>
/// Voicemeeter (the Connections tab). A channel is an input by its name in Voicemeeter (e.g. "Stereo Input 1") or an
/// output bus (A1, B1, ...). Something that fails (Voicemeeter not connected, no such channel) is logged and throws a
/// <see cref="StreamAssistException"/>.
/// </summary>
public interface IVoicemeeter
{
    /// <summary>Whether Voicemeeter is connected.</summary>
    bool IsConnected { get; }

    /// <summary>Whether a channel is muted.</summary>
    bool IsMuted(string channel);

    /// <summary>Mutes (On), unmutes (Off) or toggles a channel; gives whether it's muted now.</summary>
    bool SetMuted(string channel, Switch change);

    /// <summary>A channel's fader in dB.</summary>
    double GetGain(string channel);

    /// <summary>Sets a channel's fader in dB (kept to -60 to 12).</summary>
    void SetGain(string channel, double db);

    /// <summary>Whether an input is sent to an output bus (its A1 / B1 ... button is on).</summary>
    bool IsRouted(string input, string bus);

    /// <summary>Turns an input's output button on, off or toggles it.</summary>
    void SetRouted(string input, string bus, Switch change);

    /// <summary>Any setting by its remote API name, e.g. "Strip[0].Comp" or "Bus[3].EQ.on" (Strip[0] is the first input, Bus[0] is A1).</summary>
    double GetParameter(string name);

    /// <summary>Sets any setting by its remote API name.</summary>
    void SetParameter(string name, double value);
}
