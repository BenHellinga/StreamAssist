using System.Text.Json.Serialization;
using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Blocks.Model;



// a block that does something when run (containers and actions)
public abstract class ExecutableBlock : BlockModel
{
    // public api



    // runs this block unless it's disabled, errors are passed up to the caller
    public async Task RunAsync(TriggerContext context)
    {
        if (IsDisabled) return;
        context.CancellationToken.ThrowIfCancellationRequested();

        RaiseTriggered();
        await ExecuteAsync(context);
    }



    // runs this block and logs any error instead of passing it up, used for the top block of a tree
    public async Task SafeRunAsync(TriggerContext context)
    {
        try
        {
            await RunAsync(context);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            // stopped on purpose (e.g. the bind was removed), not an error
        }
        catch (BlockFailedException)
        {
            // the block that failed already logged why
        }
        catch (StopRunException)
        {
            // a Stop block ended the run on purpose
        }
        catch (Exception ex)
        {
            AppLog.WriteError($"{Name}: {ex.Message}");
        }
    }



    // protected



    protected abstract Task ExecuteAsync(TriggerContext context);
}



// a block that holds other blocks in its slots
public abstract class ContainerBlock : ExecutableBlock
{
    [JsonIgnore] protected override BlockGroup KindGroup => BlockGroup.Control;



    // loops call this before each pass: blocks run on the UI thread, so a loop that never waits would freeze the app.
    // It lets everything else waiting (drawing, input, other runs) go first, and stops the loop once the run is cancelled.
    // Only a run inside the UI's loop waits for it (anywhere else, e.g. the tests, nothing may be running that loop)
    protected static async Task NextPassAsync(TriggerContext context)
    {
        if (SynchronizationContext.Current is AvaloniaSynchronizationContext) await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        else await Task.Yield();

        context.CancellationToken.ThrowIfCancellationRequested();
    }
}



// a block that does one thing and has no slots
public abstract class ActionBlock : ExecutableBlock
{
    [JsonIgnore] protected override BlockGroup KindGroup => BlockGroup.Control;
}
