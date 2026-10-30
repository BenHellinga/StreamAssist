using System;
using System.Collections.ObjectModel;
using Avalonia.Threading;

namespace StreamAssist.Components.OutputConsole.Model;



// the output console. "No news is good news": it only shows things that went wrong or need the user, never successes
// or routine actions. Every line is one of three kinds, "[time] kind - message":
//   error   - something failed (a block couldn't run, a connection couldn't be made, a setting is wrong)
//   warning - something went wrong but the app carried on (a connection dropped, something was skipped on load)
//   prompt  - the user has to do something (e.g. open a login link)
// messages are short, around 5-10 words, enough to get it across, e.g. "can't send chat message, twitch not connected"
// (no "Could not ... - ...", no full stop). A block's failure starts with its name: "Round: Number 'abc' is not a number"
public static class AppLog
{
    private const int _MAX_LINES = 1000;
    public static ObservableCollection<string> Lines { get; } = [];



    public static void WriteError(string message) => WriteLine($"error - {message}");



    public static void WriteWarning(string message) => WriteLine($"warning - {message}");



    public static void WritePrompt(string message) => WriteLine($"prompt - {message}");



    // a line exactly as given: only for the Console Message block (the user's own text), everything else uses the
    // three kinds above
    public static void WriteLine(string message)
    {
        var stamped = $"[{DateTime.Now:HH:mm:ss}] {message}";

        // switch to UI thread
        Dispatcher.UIThread.Post(() =>
        {
            Lines.Add(stamped);

            while (Lines.Count > _MAX_LINES)
                Lines.RemoveAt(0);
        });
    }



    public static void Clear() => Dispatcher.UIThread.Post(Lines.Clear);
}