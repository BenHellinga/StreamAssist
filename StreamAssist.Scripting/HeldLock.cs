using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace StreamAssist.Scripting;



/// <summary>
/// A lock this run holds (from <see cref="IData.LockTableAsync"/>, <see cref="IData.LockVariableAsync"/> or
/// <see cref="IApp.LockAsync"/>): disposing it lets go, best with <c>using</c> so it's let go however the block ends:
/// <code>
/// using (await Data.LockTableAsync("quotes"))
/// {
///     // read, work it out, write back: no other run uses the table meanwhile
/// }
/// </code>
/// A lock still held when the run ends is let go then.
/// </summary>
public sealed class HeldLock : IDisposable, IAsyncDisposable
{
    private Action? _release;



    [EditorBrowsable(EditorBrowsableState.Never)]
    public HeldLock(Action release) => _release = release;



    /// <summary>Lets go of the lock (again does nothing).</summary>
    public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();



    /// <summary>Lets go of the lock (again does nothing).</summary>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
