using System;
using System.Threading;
using System.Threading.Tasks;

namespace StreamAssist.Components.Chatbot.Background;



// everything the app does on twitch (chat messages, timeouts, bans, deletes) takes turns here, at most one per interval,
// in the order they were asked for, so a busy chat can't make the account spam twitch (and get rate limited or flagged)
public sealed class TwitchActionQueue
{
    // private



    private readonly TimeSpan _interval;
    private readonly SemaphoreSlim _turn; // one action at a time, waiters are let through in order (nothing to dispose: no wait handle is ever made)
    private DateTime _nextAllowed;



    // constructor



    public TwitchActionQueue(TimeSpan interval)
    {
        _interval = interval;
        _turn = new(1, 1);
        _nextAllowed = DateTime.MinValue;
    }



    // public api



    // waits its turn (and the interval since the last action started), then runs the action
    // cancelling (e.g. the binding was removed) gives up its place in the line
    public async Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        await _turn.WaitAsync(ct);

        try
        {
            var wait = _nextAllowed - DateTime.UtcNow;
            if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);

            _nextAllowed = DateTime.UtcNow + _interval;
            return await action();
        }
        finally
        {
            _turn.Release();
        }
    }
}
