using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using StreamAssist.Components.Blocks.Containers;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Blocks.Model;



// something that can be triggered (a key, a chat command, ...), holds a root list of blocks run top to bottom (like a
// List block without its box, so a script can be rearranged in place: a block can wait at the bottom meanwhile)
public class BindableModel : ModelBase
{
    // private



    private bool _isDisabled;
    private string _label;
    private readonly BlockSlotList _root;
    private readonly CancellationTokenSource _cancellation;
    private Action? _unbindSelf;



    // public



    [JsonIgnore] public bool IsDisabled => _isDisabled;
    [JsonIgnore] public string Label => _label; // optional name shown on its key / bind box (e.g. "On Hold"), "" for none
    [JsonIgnore] public BlockSlotList Root => _root;
    [JsonIgnore] public bool HasBlock => _root.Blocks.Count > 0;



    // json



    [JsonInclude] [JsonPropertyName("isDisabled")]
    private bool JsonIsDisabled
    {
        get => _isDisabled;
        set => _isDisabled = value;
    }

    // only saved when there is one
    [JsonInclude] [JsonPropertyName("label")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private string? JsonLabel
    {
        get => _label == "" ? null : _label;
        set => _label = value ?? "";
    }

    [JsonInclude] [JsonPropertyName("root")]
    private List<PendingBlockData> JsonRoot
    {
        get => _root.GetData();
        set => _root.LoadData(value ?? []);
    }



    // events



    public event Action<bool>? IsDisabledChanged;
    public event Action? LabelChanged;
    public event Action? Changed; // anything in this bindable changed (used for saving)
    public event Action? Triggered; // it just ran (e.g. so the UI can flash it)



    // constructors



    public BindableModel()
    {
        _isDisabled = false;
        _label = "";
        _root = new("", typeof(ExecutableBlock));
        _cancellation = new();
        _unbindSelf = null;

        _root.Changed += OnRootChanged;
        _root.BlockRemoved += OnRootBlockRemoved;
    }



    // a new bindable starts with an empty On Pressed block
    public static BindableModel CreateDefault()
    {
        var bindable = new BindableModel();
        bindable.TryAddBlock(new OnPressedBlock());
        return bindable;
    }



    // triggers



    public void TriggerStart() => Trigger(TriggerPhase.Start);



    public void TriggerStop() => Trigger(TriggerPhase.Stop);



    // parameters are passed down to every block in this run (e.g. a chat command's values)
    public void TriggerOnce(IReadOnlyDictionary<string, string>? parameters = null) => Trigger(TriggerPhase.Once, parameters);



    // runs it once and finishes when it has (Run Script waiting until done): stopped by this binding's disposal or the
    // caller's cancellation. False if it didn't run (off, or no blocks); its own errors are logged and end only it
    public async Task<bool> RunOnceAsync(IReadOnlyDictionary<string, string>? parameters, CancellationToken caller)
    {
        if (_isDisabled || !HasBlock) return false;

        Triggered?.Invoke();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cancellation.Token, caller);
        await RunRootAsync(new TriggerContext(TriggerPhase.Once, linked.Token, parameters));
        return true;
    }



    // runs the tree in the background, errors are logged by SafeRunAsync
    private void Trigger(TriggerPhase phase, IReadOnlyDictionary<string, string>? parameters = null)
    {
        if (_isDisabled) return;
        if (!HasBlock) return;

        Triggered?.Invoke();
        _ = RunRootAsync(new TriggerContext(phase, _cancellation.Token, parameters));
    }



    // each root block runs in turn (waiting for the one before), and like in a List an error or a Stop ends the run
    private async Task RunRootAsync(TriggerContext context)
    {
        foreach (var block in _root.Blocks.OfType<ExecutableBlock>().ToArray())
            if (!await block.SafeRunAsync(context)) return;
    }



    // label



    // only something with blocks can be labelled (nothing bound, nothing to label)
    public void SetLabel(string label)
    {
        if (label == _label) return;
        if (!HasBlock && label != "") return;

        _label = label;
        LabelChanged?.Invoke();
        Changed?.Invoke();
    }



    // isDisabled management



    public void SetIsDisabled(bool isDisabled)
    {
        if (isDisabled == _isDisabled) return;

        _isDisabled = isDisabled;
        IsDisabledChanged?.Invoke(_isDisabled);
        Changed?.Invoke();
    }



    // block management



    // adds a block at the bottom of the root list
    public bool TryAddBlock(BlockModel block) => _root.TryInsertBlock(block, _root.Blocks.Count);



    // removes every root block, which also removes this bindable from its owner (see OnRootBlockRemoved)
    public void Clear()
    {
        foreach (var block in _root.Blocks.ToArray())
            _root.RemoveBlock(block);
    }



    // the owner (e.g. a keybind config) passes a callback that removes this bindable from it
    public void AttachUnbindSelf(Action? unbindSelf) => _unbindSelf = unbindSelf;



    // copying



    public static BindableModel DeepCopy(BindableModel bindable)
    {
        var json = JsonSerializer.Serialize(bindable);
        return JsonSerializer.Deserialize<BindableModel>(json)!;
    }



    // model callbacks



    private void OnRootChanged() => Changed?.Invoke();



    // a bindable with no root block has nothing to run, so it removes itself from its owner
    // the last block went: its label goes too, then it unbinds itself
    private void OnRootBlockRemoved(BlockModel _)
    {
        if (HasBlock) return;

        SetLabel("");
        _unbindSelf?.Invoke();
    }



    // disposal



    public override void Dispose()
    {
        _root.Changed -= OnRootChanged;
        _root.BlockRemoved -= OnRootBlockRemoved;

        _cancellation.Cancel(); // stops anything still running
        _cancellation.Dispose();

        _root.Dispose();
    }
}
