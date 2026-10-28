using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace StreamAssist.Components.Blocks.Model;



// a block that answers true or false, only fits in condition slots
public abstract class ConditionBlock : BlockModel
{
    // public



    [JsonIgnore] public override BlockCategory Category => BlockCategory.Condition;



    // public api



    // a disabled condition counts as false
    public async Task<bool> EvaluateAsync(TriggerContext context)
    {
        if (IsDisabled) return false;
        context.CancellationToken.ThrowIfCancellationRequested();

        RaiseTriggered();
        return await CheckAsync(context);
    }



    // protected



    protected abstract Task<bool> CheckAsync(TriggerContext context);
}
