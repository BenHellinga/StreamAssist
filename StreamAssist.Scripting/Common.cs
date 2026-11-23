namespace StreamAssist.Scripting;



/// <summary>Turn something on, off, or the other way round from how it is now.</summary>
public enum Switch
{
    /// <summary>Turn it on (already on: nothing happens).</summary>
    On,

    /// <summary>Turn it off (already off: nothing happens).</summary>
    Off,

    /// <summary>On if it's off, off if it's on.</summary>
    Toggle,
}
