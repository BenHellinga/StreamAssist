using System;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Core;

namespace StreamAssist.Features.Shell;



/// <summary>
/// Static, app-wide log sink. Call AppLog.WriteLine(...) from anywhere
/// (services, viewmodels, etc.) instead of Console.WriteLine / Debug.WriteLine,
/// and it'll show up in the on-screen OutputConsole.
/// </summary>
public static class AppLog
{
    // Tune this to change how much horizontal space the console takes up.
    // 0.10 = 10%, 0.15 = 15%, 0.20 = 20%, etc.
    public const double ConsoleWidthRatio = 0.10;

    private const int _MAX_LINES = 1000;

    public static ObservableCollection<string> Lines { get; } = [];



    public static void WriteLine(string message)
    {
        var stamped = $"[{DateTime.Now:HH:mm:ss}] {message}";

        // Logging can be called from any thread (services, hooks, etc.),
        // but the collection backs UI bindings, so always hop to the UI thread.
        Dispatcher.UIThread.Post(() =>
        {
            Lines.Add(stamped);

            while (Lines.Count > _MAX_LINES)
                Lines.RemoveAt(0);
        });
    }



    public static void Clear() =>
        Dispatcher.UIThread.Post(Lines.Clear);
}



/// <summary>
/// Drives the OutputConsole view. Just exposes the static AppLog buffer
/// for binding, plus a clear command.
/// </summary>
public partial class OutputConsoleViewModel : ViewModelBase
{
    public ObservableCollection<string> Lines => AppLog.Lines;



    [RelayCommand]
    private void Clear() => AppLog.Clear();
}
