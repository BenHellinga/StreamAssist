using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Chatbot.Model;



// what happens after a Match Text row or Parameter matches: run a binding, OR continue matching into a chat block
// never both, the model refuses the second one (the UI hides it too)
// redeems and events use binding-only targets (allowsBlocks false), their child slot always refuses blocks
public sealed class ChatTarget : BlockSlotBase
{
    // private



    private readonly BlockSlot _child;
    private readonly bool _allowsBlocks;
    private bool _endsHere;
    private BindableModel? _bindable;



    // public



    public BlockSlot Child => _child; // continue matching here (another Match Text or a Parse Parameter)
    public BindableModel? Bindable => _bindable; // or run this
    public bool AllowsBlocks => _allowsBlocks;
    public bool EndsHere => _endsHere; // nothing is left to match (e.g. after the rest of the message), so no new blocks
    public bool IsEmpty => _child.Block == null && _bindable == null;

    public override IReadOnlyList<BlockModel> Blocks => [];
    public override IReadOnlyList<BlockSlotBase> InnerSlots => [_child];



    // events



    public event Action? BindableChanged; // bound, unbound, or a different binding
    public event Action? ContentsChanged; // the binding or the child block came or went, or EndsHere changed



    // constructor



    public ChatTarget(bool allowsBlocks = true) : base("", typeof(ChatBlock))
    {
        _child = new("", typeof(ChatBlock));
        _allowsBlocks = allowsBlocks;
        _child.Lock = () => !_allowsBlocks ? "this only runs a binding"
            : _endsHere ? "the rest of the message is already read"
            : _bindable != null ? "this row already runs a binding" : null;
        _endsHere = false;
        _bindable = null;

        _child.Changed += RaiseChanged;
        _child.BlockChanged += OnChildBlockChanged;
    }



    // set by the owner when nothing can be left to match after it, a block already there stays (it's never dropped quietly)
    public void SetEndsHere(bool endsHere)
    {
        if (endsHere == _endsHere) return;

        _endsHere = endsHere;
        ContentsChanged?.Invoke();
    }



    // binding management



    public bool TryBind(BindableModel bindable)
    {
        if (_child.Block != null)
        {
            AppLog.WriteError("can't bind row, it continues into a block");
            return false;
        }

        if (_bindable != null)
        {
            AppLog.WriteError("can't bind row, already bound");
            return false;
        }

        Bind(bindable);
        return true;
    }



    private void Bind(BindableModel bindable)
    {
        _bindable = bindable;
        _bindable.AttachUnbindSelf(Unbind); // a binding whose blocks are all removed goes away, like an empty key
        _bindable.Changed += RaiseChanged;

        BindableChanged?.Invoke();
        ContentsChanged?.Invoke();
        RaiseChanged();
    }



    public void Unbind()
    {
        if (_bindable == null) return;

        var old = _bindable;
        _bindable = null;
        old.Changed -= RaiseChanged;
        old.AttachUnbindSelf(null);

        BindableChanged?.Invoke();
        ContentsChanged?.Invoke();
        RaiseChanged();
        old.Dispose();
    }



    public override void RemoveBlock(BlockModel block) => _child.RemoveBlock(block);



    // matching



    // runs the binding (with {rest} = whatever is left of the message), or keeps matching into the child block
    // a disabled binding or block counts as no match, so the next row gets a turn
    public bool TryMatch(ChatMatchState state)
    {
        if (_bindable != null)
            return TryRun(new Dictionary<string, string>(state.Parameters) { ["rest"] = state.Text[state.Position..] });

        return _child.Block is ChatBlock child && !child.IsDisabled && child.TryMatch(state);
    }



    // runs the binding with these parameters, false if there's no binding or it's disabled (nothing to report, so no log)
    public bool TryRun(IReadOnlyDictionary<string, string> parameters)
    {
        if (_bindable == null || _bindable.IsDisabled) return false;

        _bindable.TriggerOnce(parameters);
        return true;
    }



    // model callbacks



    private void OnChildBlockChanged(BlockModel? _, BlockModel? __) => ContentsChanged?.Invoke();



    // json (owners save the target through these)



    public ChatTargetData GetData() => new() { Bindable = _bindable, Child = _child.GetData() };



    // only runs when loading, if a file somehow has both, the binding wins
    public void LoadData(ChatTargetData data)
    {
        if (data.Bindable != null && data.Child != null)
            AppLog.WriteWarning("row had a binding and a block, block dropped");

        if (data.Bindable != null)
        {
            if (data.Bindable.HasBlock) Bind(data.Bindable);
            else data.Bindable.Dispose();
            return;
        }

        _child.LoadData(data.Child);
    }



    // disposal



    public override void Dispose()
    {
        _child.Changed -= RaiseChanged;
        _child.BlockChanged -= OnChildBlockChanged;
        _child.Dispose();

        if (_bindable != null)
        {
            _bindable.Changed -= RaiseChanged;
            _bindable.Dispose();
        }
    }
}



// how a target is saved
public class ChatTargetData
{
    [JsonPropertyName("bindable")]
    public BindableModel? Bindable { get; set; }

    [JsonPropertyName("child")]
    public PendingBlockData? Child { get; set; }
}
