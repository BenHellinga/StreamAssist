using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace StreamAssist.Components.Blocks.Model;



// a block that answers true or false (a bool value block), fits condition slots and bool value slots
// a disabled or empty condition counts as false
public abstract class ConditionBlock : ValueBlock<bool>
{
    // public






    // protected



    protected sealed override Task<bool> ComputeAsync(TriggerContext context) => CheckAsync(context);



    protected abstract Task<bool> CheckAsync(TriggerContext context);



    // for a check that's false instead of stopping the run when it can't be done (e.g. a key that isn't a key):
    // the API's error is logged under this block's name and it reads false
    protected bool Quietly(System.Func<bool> check)
    {
        try
        {
            return check();
        }
        catch (StreamAssist.Scripting.StreamAssistException ex)
        {
            if (!ex.IsLogged) StreamAssist.Components.OutputConsole.Model.AppLog.WriteError($"{Name}: {ex.Message}");
            return false;
        }
    }



    // condition slots take any bool value block: conditions, and "any type" blocks set to Bool (e.g. Convert)
    public static bool IsCondition(BlockModel? block) => block is ValueBlock { Kind: ValueKind.Bool };



    // a missing, disabled or non-bool block counts as false
    public static async Task<bool> EvaluateConditionAsync(BlockModel? block, TriggerContext context) =>
        block is ValueBlock { Kind: ValueKind.Bool, IsDisabled: false } value && (bool)await value.EvaluateObjectAsync(context);
}
