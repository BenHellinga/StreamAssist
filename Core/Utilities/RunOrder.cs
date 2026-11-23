using System;
using System.Threading.Tasks;

namespace StreamAssist.Core.Utilities;



// keeps one thing's runs (a binding's) starting in the order they were triggered when they're on background threads:
// each waits until the one before has got going, then they carry on side by side (UiThread.Start)
public sealed class RunOrder
{
    private readonly object _changing = new();
    private Task _lastGoing = Task.CompletedTask;



    // takes its place in line now, on the caller's thread (where the order is), then runs on a background thread
    public Task Start(Func<Task> run)
    {
        var going = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task previous;
        lock (_changing)
        {
            previous = _lastGoing;
            _lastGoing = going.Task;
        }

        return Task.Run(async () =>
        {
            await previous;

            // the run's task comes back at its first wait (or when it's done): from then on the next one can start
            Task running;
            try
            {
                running = run();
            }
            finally
            {
                going.SetResult();
            }
            await running;
        });
    }
}
