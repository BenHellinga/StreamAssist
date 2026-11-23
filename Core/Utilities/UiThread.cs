using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace StreamAssist.Core.Utilities;



// runs (blocks, C# scripts) happen on background threads so one that's busy or stuck never freezes the app, while
// everything else (models, managers, the UI) stays on the UI thread. This is the one way between them: a run starts
// with Start, and anything it does to the app goes through Run / RunAsync, which do the work on the UI thread and hand
// the result back to the run's thread
//
// off (the default, and in the tests, where nothing runs a UI loop for posted work): runs start on the caller's
// thread and everything is done in place, as if there were one thread
public static class UiThread
{
    // set at startup, before anything runs
    public static bool RunsInBackground { get; set; }



    // whether work for the UI thread can be done right here, now
    private static bool IsHere => !RunsInBackground || Dispatcher.UIThread.CheckAccess();



    // runs



    // starts a run: on a background thread (the caller goes on at once), or in place while it's off
    public static Task Start(Func<Task> run) => RunsInBackground ? Task.Run(run) : run();



    // the same, but after the run started before it with this order has got going (reached its first wait, or ended):
    // a key's press and release, started a moment apart, then do what they do in that order, as they would on one thread
    public static Task Start(RunOrder order, Func<Task> run) => RunsInBackground ? order.Start(run) : run();



    // a callback a manager calls later on the UI thread (a timer ending, a wheel stopping), made to run on a background
    // thread instead, as part of the run that set it up (its cancellation, limits and locks: the context it had now)
    public static Action? Later(Action? callback)
    {
        if (callback == null || !RunsInBackground) return callback;

        var context = ExecutionContext.Capture();
        return () =>
        {
            if (context == null) Task.Run(callback);
            else ExecutionContext.Run(context, _ => Task.Run(callback), null);
        };
    }

    public static Action<T1>? Later<T1>(Action<T1>? callback) =>
        callback == null || !RunsInBackground ? callback : Wrap(callback);

    public static Action<T1, T2>? Later<T1, T2>(Action<T1, T2>? callback) =>
        callback == null || !RunsInBackground ? callback : Wrap(callback);



    // the work on the UI thread



    // does it on the UI thread and waits for it (from a run: its thread waits, the UI never does)
    public static void Run(Action work) => Run(() => { work(); return true; });



    public static T Run<T>(Func<T> work) => IsHere ? work() : RunAsync(() => Task.FromResult(work())).GetAwaiter().GetResult();



    // starts it on the UI thread; whoever awaits it goes on on their own thread afterwards, never the UI's
    public static Task RunAsync(Func<Task> work) =>
        IsHere ? work() : RunAsync<bool>(async () => { await work(); return true; });



    public static Task<T> RunAsync<T>(Func<Task<T>> work)
    {
        if (IsHere) return work();

        var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (ExecutionContext.SuppressFlow()) // the app's work doesn't belong to the run (a timer it starts outlives it)
        {
            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    result.TrySetResult(await work());
                }
                catch (OperationCanceledException ex)
                {
                    result.TrySetCanceled(ex.CancellationToken);
                }
                catch (Exception ex)
                {
                    result.TrySetException(ex);
                }
            });
        }
        return result.Task;
    }



    // tells the UI something without waiting (e.g. a block flashing): in place on the UI thread, else queued
    public static void Post(Action work)
    {
        if (IsHere) work();
        else Dispatcher.UIThread.Post(work);
    }



    // helpers



    private static Action<T1> Wrap<T1>(Action<T1> callback)
    {
        var context = ExecutionContext.Capture();
        return a =>
        {
            if (context == null) Task.Run(() => callback(a));
            else ExecutionContext.Run(context, _ => Task.Run(() => callback(a)), null);
        };
    }



    private static Action<T1, T2> Wrap<T1, T2>(Action<T1, T2> callback)
    {
        var context = ExecutionContext.Capture();
        return (a, b) =>
        {
            if (context == null) Task.Run(() => callback(a, b));
            else ExecutionContext.Run(context, _ => Task.Run(() => callback(a, b)), null);
        };
    }
}
