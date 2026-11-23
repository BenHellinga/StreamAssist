using System.Collections.Generic;

namespace StreamAssist.Scripting;



/// <summary>
/// What's held right now on keyboards, mice, controllers and microphones (works while the app isn't focused). A device
/// is its name as the Input tab shows it, null for any. One that isn't connected logs a warning and counts as not held.
/// </summary>
public interface IInput
{
    /// <summary>Whether a key is held, by its name as the Input tab shows it (e.g. "F13", "Numpad 1"); left and right Shift/Ctrl/Alt count as the same key.</summary>
    bool IsKeyDown(string key, string? keyboard = null);

    /// <summary>Whether a mouse button is held (e.g. "Left", "Right", "Middle").</summary>
    bool IsMouseButtonDown(string button, string? mouse = null);

    /// <summary>Whether a controller button is held (triggers and stick directions count once pushed past halfway).</summary>
    bool IsControllerButtonDown(string button, string? controller = null);

    /// <summary>Whether you're talking into a microphone, as its Microphone box on the Input tab decides (its threshold and hold).</summary>
    bool IsTalking(string? microphone = null);

    /// <summary>The connected keyboards' names.</summary>
    IReadOnlyList<string> Keyboards { get; }

    /// <summary>The connected mice's names.</summary>
    IReadOnlyList<string> Mice { get; }

    /// <summary>The connected controllers' names.</summary>
    IReadOnlyList<string> Controllers { get; }

    /// <summary>The microphones' names.</summary>
    IReadOnlyList<string> Microphones { get; }

    /// <summary>Every mouse button's name.</summary>
    IReadOnlyList<string> MouseButtons { get; }

    /// <summary>Every controller button's name.</summary>
    IReadOnlyList<string> ControllerButtons { get; }
}
