using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace StreamAssist.Components.Blocks.Model;



// a block that returns a value (text, a number, true/false, ...), it goes in value slots (and bool ones in condition slots)
// it doesn't do anything by itself, the block holding it asks for the value when it runs
public abstract class ValueBlock : BlockModel
{
    [JsonIgnore] public abstract ValueKind Kind { get; }



    // the value, as whatever type Kind says (for code that handles any kind, like Equals)
    public abstract Task<object> EvaluateObjectAsync(TriggerContext context);
}



// a value block returning T (string, int, double, bool or char)
public abstract class ValueBlock<T> : ValueBlock where T : notnull
{
    // public



    [JsonIgnore] public override ValueKind Kind => ValueTypes.KindOf(typeof(T));

    // a general value block is listed by what it gives: text, true/false (Logic) or a number (Math)
    [JsonIgnore] protected override BlockGroup KindGroup => Kind switch
    {
        ValueKind.Text or ValueKind.Char => BlockGroup.Text,
        ValueKind.Bool => BlockGroup.Logic,
        _ => BlockGroup.Math,
    };



    // public api



    // flashes like a running block, a disabled one returns DisabledValue (value slots use their typed value instead)
    public async Task<T> EvaluateAsync(TriggerContext context)
    {
        if (IsDisabled) return DisabledValue;
        context.CancellationToken.ThrowIfCancellationRequested();

        RaiseTriggered();
        return await ComputeAsync(context);
    }



    public override async Task<object> EvaluateObjectAsync(TriggerContext context) => await EvaluateAsync(context);



    // protected



    protected virtual T DisabledValue => default!;



    // throw Fail("why") when the value can't be worked out, it's logged here and stops the run
    protected abstract Task<T> ComputeAsync(TriggerContext context);
}
