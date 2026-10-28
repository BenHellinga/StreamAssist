using System.Text.Json.Serialization;
using System;
using System.Threading.Tasks;
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
        catch (Exception ex)
        {
            AppLog.WriteError($"Block '{Name}' stopped - {ex.Message}");
        }
    }



    // protected



    protected abstract Task ExecuteAsync(TriggerContext context);
}



// a block that holds other blocks in its slots
public abstract class ContainerBlock : ExecutableBlock
{
    [JsonIgnore] public override BlockCategory Category => BlockCategory.Container;
}



// a block that does one thing and has no slots
public abstract class ActionBlock : ExecutableBlock
{
    [JsonIgnore] public override BlockCategory Category => BlockCategory.Action;
}
