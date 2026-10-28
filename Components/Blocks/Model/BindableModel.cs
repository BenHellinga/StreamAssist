using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using StreamAssist.Components.Blocks.Containers;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Blocks.Model;



// something that can be triggered (a key, a chat command, ...), holds one root block
public class BindableModel : ModelBase
{
    // private



    private bool _isDisabled;
    private readonly BlockSlot _root;
    private readonly CancellationTokenSource _cancellation;
    private Action? _unbindSelf;



    // public



    [JsonIgnore] public bool IsDisabled => _isDisabled;
    [JsonIgnore] public BlockSlot Root => _root;
    [JsonIgnore] public bool HasBlock => _root.Block != null;



    // json



    [JsonInclude] [JsonPropertyName("isDisabled")]
    private bool JsonIsDisabled
    {
        get => _isDisabled;
        set => _isDisabled = value;
    }

    [JsonInclude] [JsonPropertyName("root")]
    private PendingBlockData? JsonRoot
    {
        get => _root.GetData();
        set => _root.LoadData(value);
    }



    // events



    public event Action<bool>? IsDisabledChanged;
    public event Action? Changed; // anything in this bindable changed (used for saving)



    // constructors



    public BindableModel()
    {
        _isDisabled = false;
        _root = new("", typeof(ExecutableBlock));
        _cancellation = new();
        _unbindSelf = null;

        _root.Changed += OnRootChanged;
        _root.BlockChanged += OnRootBlockChanged;
    }



    // a new bindable starts with an empty On Pressed block
    public static BindableModel CreateDefault()
    {
        var bindable = new BindableModel();
        bindable._root.TrySetBlock(new OnPressedBlock());
        return bindable;
    }



    // triggers



    public void TriggerStart() => Trigger(TriggerPhase.Start);



    public void TriggerStop() => Trigger(TriggerPhase.Stop);



    // parameters are passed down to every block in this run (e.g. a chat command's values)
    public void TriggerOnce(IReadOnlyDictionary<string, string>? parameters = null) => Trigger(TriggerPhase.Once, parameters);



    // runs the tree in the background, errors are logged by SafeRunAsync
    private void Trigger(TriggerPhase phase, IReadOnlyDictionary<string, string>? parameters = null)
    {
        if (_isDisabled) return;
        if (_root.Block is not ExecutableBlock root) return;

        _ = root.SafeRunAsync(new TriggerContext(phase, _cancellation.Token, parameters));
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



    // removes the root block, which also removes this bindable from its owner (see OnRootBlockChanged)
    public void Clear()
    {
        if (_root.Block != null) _root.RemoveBlock(_root.Block);
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
    private void OnRootBlockChanged(BlockModel? _, BlockModel? newBlock)
    {
        if (newBlock == null) _unbindSelf?.Invoke();
    }



    // disposal



    public override void Dispose()
    {
        _root.Changed -= OnRootChanged;
        _root.BlockChanged -= OnRootBlockChanged;

        _cancellation.Cancel(); // stops anything still running
        _cancellation.Dispose();

        _root.Dispose();
    }
}
