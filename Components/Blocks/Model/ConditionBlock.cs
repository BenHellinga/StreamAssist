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



    // condition slots take any bool value block: conditions, and "any type" blocks set to Bool (e.g. Convert)
    public static bool IsCondition(BlockModel? block) => block is ValueBlock { Kind: ValueKind.Bool };



    // a missing, disabled or non-bool block counts as false
    public static async Task<bool> EvaluateConditionAsync(BlockModel? block, TriggerContext context) =>
        block is ValueBlock { Kind: ValueKind.Bool, IsDisabled: false } value && (bool)await value.EvaluateObjectAsync(context);
}
