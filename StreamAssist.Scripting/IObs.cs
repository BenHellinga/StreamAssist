using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StreamAssist.Scripting;



/// <summary>
/// OBS (obs-websocket, the Connections tab). Names are as OBS shows them. A scene left null or empty means the scene
/// that's live right now. A request that fails (OBS not connected, no such source) is logged and throws a
/// <see cref="StreamAssistException"/>.
/// </summary>
public interface IObs
{
    /// <summary>Whether OBS is connected.</summary>
    bool IsConnected { get; }



    // scenes and sources



    /// <summary>The name of the scene that's live.</summary>
    Task<string> GetSceneAsync();

    /// <summary>Switches to a scene.</summary>
    Task SetSceneAsync(string scene);

    /// <summary>Every scene, top to bottom as OBS lists them.</summary>
    Task<IReadOnlyList<string>> GetScenesAsync();

    /// <summary>Every source in a scene, top to bottom.</summary>
    Task<IReadOnlyList<string>> GetSourcesAsync(string? scene = null);

    /// <summary>Whether a source is showing in a scene (its eye is on).</summary>
    Task<bool> IsSourceVisibleAsync(string source, string? scene = null);

    /// <summary>Shows (On), hides (Off) or toggles a source in a scene.</summary>
    Task SetSourceVisibleAsync(string source, Switch change, string? scene = null);



    // where sources are



    /// <summary>Where a source is in a scene, its size, rotation or crop (pixels, degrees, 1 = its own size for scale).</summary>
    Task<double> GetTransformAsync(string source, ObsTransform setting, string? scene = null);

    /// <summary>Moves, resizes, rotates or crops a source (Width and Height change its scale).</summary>
    Task SetTransformAsync(string source, ObsTransform setting, double value, string? scene = null);

    /// <summary>Resizes a source to this many pixels while one of its points stays still (e.g. BottomRight).</summary>
    Task ResizeAsync(string source, double width, double height, ObsAnchor keepFixed = ObsAnchor.TopLeft, string? scene = null);

    /// <summary>Grows a source by a percentage (20 = 20% bigger, -20 smaller) while one of its points stays still.</summary>
    Task ScaleByAsync(string source, double percent, ObsAnchor keepFixed = ObsAnchor.TopLeft, string? scene = null);



    // audio



    /// <summary>An audio source's volume in dB, like the OBS mixer (0 is full volume, lower is quieter).</summary>
    Task<double> GetVolumeAsync(string source);

    /// <summary>Sets an audio source's volume in dB (-100 to 26).</summary>
    Task SetVolumeAsync(string source, double db);

    /// <summary>Whether an audio source is muted.</summary>
    Task<bool> IsMutedAsync(string source);

    /// <summary>Mutes (On), unmutes (Off) or toggles an audio source.</summary>
    Task SetMutedAsync(string source, Switch change);



    // filters, outputs and settings



    /// <summary>Turns a filter on a source (or a scene) on, off or toggles it.</summary>
    Task SetFilterAsync(string source, string filter, Switch change);

    /// <summary>Whether an output is running, and for how long.</summary>
    Task<ObsOutputStatus> GetOutputAsync(ObsOutput output);

    /// <summary>Starts (On), stops (Off) or toggles an output.</summary>
    Task SetOutputAsync(ObsOutput output, Switch change);

    /// <summary>Saves the replay buffer's last moments to a file (the replay buffer has to be running).</summary>
    Task SaveReplayAsync();

    /// <summary>Any setting of a source, like its Properties window (e.g. "text", "url", "file"): text, a number, true/false, or the raw json of anything else.</summary>
    Task<object> GetSourceSettingAsync(string source, string setting);

    /// <summary>Sets any setting of a source (it keeps the kind the setting already is).</summary>
    Task SetSourceSettingAsync(string source, string setting, object value);

    /// <summary>Writes into a text source.</summary>
    Task SetTextAsync(string source, string text);
}



/// <summary>Something about where a source is.</summary>
public enum ObsTransform { PositionX, PositionY, Width, Height, ScaleX, ScaleY, Rotation, CropLeft, CropRight, CropTop, CropBottom }



/// <summary>The point of a source that stays still while it's resized.</summary>
public enum ObsAnchor { TopLeft, Top, TopRight, Left, Center, Right, BottomLeft, Bottom, BottomRight }



/// <summary>One of OBS's outputs.</summary>
public enum ObsOutput { Recording, Streaming, ReplayBuffer, VirtualCamera }



/// <summary>Whether an output is running and for how long (zero while it isn't).</summary>
public sealed record ObsOutputStatus(bool IsActive, TimeSpan Time);
