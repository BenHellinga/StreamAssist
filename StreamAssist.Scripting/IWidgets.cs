using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StreamAssist.Scripting;



/// <summary>
/// The widgets drawn on stream (the Widgets tab), found by name in the widgets config in use. An unknown widget, or
/// one of another kind, is an error (a <see cref="StreamAssistException"/>). Files are inside Assets (e.g. "Images/hat.png").
/// Callbacks run when the widget gets there, while this run carries on.
/// </summary>
public interface IWidgets
{
    /// <summary>Shows (On), hides (Off) or toggles a widget on stream (hidden, it still runs).</summary>
    void SetVisible(string widget, Switch change);



    // wheels



    /// <summary>Adds an option (saved with the wheel). Weight is how likely it is and how wide its slice; color like #FF8800, empty takes the next one.</summary>
    void AddWheelOption(string wheel, string option, double weight = 1, string color = "");

    /// <summary>Takes an option off (by its text, case ignored).</summary>
    void RemoveWheelOption(string wheel, string option);

    /// <summary>Takes every option off.</summary>
    void ClearWheel(string wheel);

    /// <summary>The wheel's options, top to bottom.</summary>
    IReadOnlyList<string> GetWheelOptions(string wheel);

    /// <summary>The option it last landed on (empty until it has spun since the app started).</summary>
    string GetWheelWinner(string wheel);

    /// <summary>Whether it's spinning.</summary>
    bool IsWheelSpinning(string wheel);

    /// <summary>Spins it (the winner is picked now, by weight); <paramref name="whenDone"/> gets the winner once it has stopped and shown it.</summary>
    void SpinWheel(string wheel, Func<string, Task>? whenDone = null);



    // plinko



    /// <summary>
    /// Drops balls (one at a time). <paramref name="whenEachLands"/> gets each ball's bucket and number as it lands;
    /// <paramref name="whenDone"/> gets every bucket (in the order they landed) and the result once all have.
    /// </summary>
    void DropPlinko(string plinko, int balls, Func<string, int, Task>? whenEachLands = null, Func<IReadOnlyList<string>, string, Task>? whenDone = null);

    /// <summary>Whether balls are falling or waiting to drop.</summary>
    bool IsPlinkoDropping(string plinko);

    /// <summary>The last finished drop: every bucket in landing order, and its result (most landed in, or the sum, as the widget is set).</summary>
    PlinkoResult GetPlinkoResult(string plinko);



    // image party



    /// <summary>Adds an image, gif or video; size wide and moving speed pixels a second (null: the widget's own).</summary>
    void AddPartyItem(string party, string file, double? size = null, double? speed = null, bool muted = false);

    /// <summary>Takes every copy of a file off (case ignored).</summary>
    void RemovePartyItems(string party, string file);

    /// <summary>Takes every item off.</summary>
    void ClearParty(string party);



    // mic bounce



    /// <summary>How high it rises at full loudness, in pixels (0 to 2000).</summary>
    void SetBounceHeight(string bounce, double pixels);

    /// <summary>Swaps its idle or talking image (an empty talking image uses the idle one).</summary>
    void SetBounceImage(string bounce, string file, bool talking = false);



    // clips



    /// <summary>
    /// Plays a random clip of a channel (one of its most viewed that fits the widget's settings) and gives it, or null
    /// when none fit; <paramref name="whenDone"/> runs when it ends.
    /// </summary>
    Task<TwitchClip?> PlayClipAsync(string clipWidget, string channel, Func<Task>? whenDone = null);

    /// <summary>Whether a clip is playing.</summary>
    bool IsClipPlaying(string clipWidget);
}



/// <summary>A plinko drop's buckets, in the order they landed, and its result.</summary>
public sealed record PlinkoResult(IReadOnlyList<string> Buckets, string Result);
