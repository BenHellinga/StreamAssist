using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Blocks.Model;



// a value block that can return any of several kinds, picked from its dropdown (e.g. Convert, Get Parameter)
// dropped into a slot it switches to the kind the slot wants (if it doesn't already fit), and changing the dropdown
// to a kind its slot can't take is refused
public abstract class AdaptableValueBlock : ValueBlock
{
    // private



    private ValueKind _kind;



    // public



    [JsonIgnore] public override ValueKind Kind => _kind;
    [JsonIgnore] public override BlockCategory Category => BlockCategory.Any;
    [JsonIgnore] public abstract IReadOnlyList<ValueKind> AllowedKinds { get; }



    // events



    public event Action? KindChanged;



    // constructor



    protected AdaptableValueBlock(ValueKind kind)
    {
        _kind = kind;
    }



    // public api



    public bool CanBe(ValueKind kind) => AllowedKinds.Contains(kind);



    // changes what it returns, refused (and logged) if it can't return that or the slot it's in can't take it
    public bool TrySetKind(ValueKind kind)
    {
        if (kind == _kind) return true;

        if (!CanBe(kind))
        {
            AppLog.WriteError($"{Name}: can't give {ValueTypes.Describe(kind)}");
            return false;
        }

        var slot = ParentSlot;
        if (slot != null && !WithKind(kind, () => slot.Fits(this)))
        {
            var wanted = slot.WantedKind is { } wantedKind ? ValueTypes.Describe(wantedKind) : "another type";
            AppLog.WriteError($"{Name}: can't be {kind}, its slot needs {wanted}");
            return false;
        }

        SetKind(kind);
        return true;
    }



    // the value, of type Kind (callers check IsDisabled, a disabled block's slot uses its fallback)
    public override async Task<object> EvaluateObjectAsync(TriggerContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();

        RaiseTriggered();
        return await ComputeAsync(context);
    }



    // slots use these while placing it



    internal void SetKindForPlacement(ValueKind kind) => SetKind(kind);



    // runs a check as if it returned another kind (nothing is raised, nothing is kept)
    internal bool WithKind(ValueKind kind, Func<bool> check)
    {
        var old = _kind;
        _kind = kind;

        try
        {
            return check();
        }
        finally
        {
            _kind = old;
        }
    }



    // protected



    // for the subclass's dropdown property (e.g. "To"), the kind's name, setting it goes through TrySetKind
    protected string KindName
    {
        get => _kind.ToString();
        set
        {
            if (Enum.TryParse<ValueKind>(value, out var kind)) TrySetKind(kind);
        }
    }



    // must return a value of type Kind, throw Fail("why") when it can't
    protected abstract Task<object> ComputeAsync(TriggerContext context);



    // helpers



    private void SetKind(ValueKind kind)
    {
        if (kind == _kind) return;

        _kind = kind;
        KindChanged?.Invoke();
        RaiseChanged();
    }
}
