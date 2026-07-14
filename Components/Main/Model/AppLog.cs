using System;
using System.Collections.ObjectModel;
using Avalonia.Threading;

namespace StreamAssist.Components.Main.Model;



public static class AppLog
{
    private const int _MAX_LINES = 1000;
    public static ObservableCollection<string> Lines { get; } = [];



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



    public static void WriteError(string message)
    {
        WriteLine($"Error: {message}");
    }



    public static void WriteWarning(string message)
    {
        WriteLine($"Warning: {message}");
    }



    public static void Clear() => Dispatcher.UIThread.Post(Lines.Clear);
}