using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using StreamAssist.Components.Blocks.Containers;
using StreamAssist.Core.Mvvm;
using StreamAssist.Core.Utilities;

namespace StreamAssist.Components.Blocks.Model;



// how a binding does what it does: its blocks, or its C# script (it keeps both, only the one picked runs)
public enum BindableMode { Blocks, CSharp }



// how far a binding's C# script may go before it's stopped, in seconds (null: no limit): a loop going on that long
// without waiting, the whole run, and waiting for a locked table, variable or lock
public sealed record ScriptLimits(double? LoopSeconds, double? RunSeconds, double? LockWaitSeconds)
{
    public static readonly ScriptLimits Default = new(5, null, 10);

    public const double MaxSeconds = 86_400; // a day: anything longer is as good as no limit
}



// what runs a binding's C# script (the CSharp component's ScriptManager, set at startup), and makes and copies script files
public interface IBindableScripts
{
    string NewScriptId();
    void CopyScript(string fromId, string toId);
    Task RunAsync(string scriptId, TriggerContext context, ScriptLimits limits); // logs its own errors, never throws (except cancellation)
}



// something that can be triggered (a key, a chat command, ...), holds a root list of blocks run top to bottom (like a
// List block without its box, so a script can be rearranged in place: a block can wait at the bottom meanwhile), and
// optionally a C# script (Config/Scripts/<id>.cs), which runs instead while the mode is CSharp
public class BindableModel : ModelBase, IJsonOnDeserialized
{
    // every binding with a script, by its id: a second one with the same id is a copy (pasted, a config copied), which
    // gets its own copy of the file so editing one never changes the other



    private static readonly Dictionary<string, BindableModel> ScriptOwners = [];



    // set at startup (and by tests), null: scripts can't run, a C# binding logs that it can't
    public static IBindableScripts? Scripts { get; set; }




    // private



    private bool _isDisabled;
    private string _label;
    private BindableMode _mode;
    private string? _scriptId;
    private ScriptLimits _scriptLimits;
    private readonly BlockSlotList _root;
    private readonly CancellationTokenSource _cancellation;
    private readonly RunOrder _runOrder; // its runs start in the order they were triggered (a quick press and release)
    private readonly CancellationToken _token; // kept apart: a run on another thread may read it while the binding is disposed (cancelled first)
    private Action? _unbindSelf;



    // public



    [JsonIgnore] public bool IsDisabled => _isDisabled;
    [JsonIgnore] public string Label => _label; // optional name shown on its key / bind box (e.g. "On Hold"), "" for none
    [JsonIgnore] public BlockSlotList Root => _root;
    [JsonIgnore] public bool HasBlock => _root.Blocks.Count > 0;
    [JsonIgnore] public BindableMode Mode => _mode;
    [JsonIgnore] public bool IsCSharp => _mode == BindableMode.CSharp;
    [JsonIgnore] public string? ScriptId => _scriptId; // its script file's name (without .cs), null until it's switched to C# once
    [JsonIgnore] public ScriptLimits ScriptLimits => _scriptLimits;

    // something worth keeping: blocks, or a script (even while it's in Blocks mode, so switching back never loses it)
    [JsonIgnore] public bool HasContent => HasBlock || _scriptId != null;



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

    // only saved when it's C# ("csharp")
    [JsonInclude] [JsonPropertyName("mode")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private string? JsonMode
    {
        get => _mode == BindableMode.CSharp ? "csharp" : null;
        set => _mode = value == "csharp" ? BindableMode.CSharp : BindableMode.Blocks;
    }

    // only saved when it has one
    [JsonInclude] [JsonPropertyName("script")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private string? JsonScript
    {
        get => _scriptId;
        set => _scriptId = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    // only saved when they aren't the default ones; 0 is no limit
    [JsonInclude] [JsonPropertyName("limits")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private ScriptLimitsData? JsonLimits
    {
        get => _scriptLimits == ScriptLimits.Default ? null : new(_scriptLimits.LoopSeconds ?? 0, _scriptLimits.RunSeconds ?? 0, _scriptLimits.LockWaitSeconds ?? 0);
        set => _scriptLimits = value == null ? ScriptLimits.Default : new(Limit(value.Loop), Limit(value.Run), Limit(value.LockWait));
    }

    private sealed record ScriptLimitsData(
        [property: JsonPropertyName("loop")] double Loop,
        [property: JsonPropertyName("run")] double Run,
        [property: JsonPropertyName("lockWait")] double LockWait);

    private static double? Limit(double seconds) => seconds > 0 && seconds <= ScriptLimits.MaxSeconds ? seconds : null;

    [JsonInclude] [JsonPropertyName("root")]
    private List<PendingBlockData> JsonRoot
    {
        get => _root.GetData();
        set => _root.LoadData(value ?? []);
    }



    // events



    public event Action<bool>? IsDisabledChanged;
    public event Action? ModeChanged; // switched between blocks and C#
    public event Action? ScriptLimitsChanged;
    public event Action? LabelChanged;
    public event Action? Changed; // anything in this bindable changed (used for saving)
    public event Action? Triggered; // it just ran (e.g. so the UI can flash it)



    // constructors



    public BindableModel()
    {
        _isDisabled = false;
        _label = "";
        _mode = BindableMode.Blocks;
        _scriptId = null;
        _scriptLimits = ScriptLimits.Default;
        _root = new("", typeof(ExecutableBlock));
        _cancellation = new();
        _token = _cancellation.Token;
        _runOrder = new();
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
    // caller's cancellation. False if it didn't run (off, or no blocks); its own errors are logged and end only it.
    // Like every run it's on a background thread (UiThread), the caller only waits for it
    public async Task<bool> RunOnceAsync(IReadOnlyDictionary<string, string>? parameters, CancellationToken caller)
    {
        if (_isDisabled || !CanRun) return false;

        RaiseTriggered();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_token, caller);
        try
        {
            var context = new TriggerContext(TriggerPhase.Once, linked.Token, parameters);
            await UiThread.Start(_runOrder, () => RunAsync(context));
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            // stopped on purpose (the binding was removed, or whatever waited for it was), not an error
        }
        return true;
    }



    // runs on a background thread, errors are logged by SafeRunAsync / the script runner
    private void Trigger(TriggerPhase phase, IReadOnlyDictionary<string, string>? parameters = null)
    {
        if (_isDisabled) return;
        if (!CanRun) return;

        RaiseTriggered();
        var context = new TriggerContext(phase, _token, parameters);
        _ = UiThread.Start(_runOrder, () => RunInBackgroundAsync(context));
    }



    // flashes it in the UI (it may be run from a background thread, e.g. by another run's Run Script)
    private void RaiseTriggered() => UiThread.Post(() => Triggered?.Invoke());



    // what runs is what's picked: the blocks (if there are any) or the script
    private bool CanRun => IsCSharp ? _scriptId != null : HasBlock;



    private async Task RunInBackgroundAsync(TriggerContext context)
    {
        try
        {
            await RunAsync(context);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            // stopped on purpose
        }
    }



    private Task RunAsync(TriggerContext context)
    {
        if (!IsCSharp) return RunRootAsync(context);

        if (Scripts is { } scripts) return scripts.RunAsync(_scriptId!, context, _scriptLimits);
        StreamAssist.Components.OutputConsole.Model.AppLog.WriteError("can't run c# script, scripts not set up");
        return Task.CompletedTask;
    }



    // each root block runs in turn (waiting for the one before), and like in a List an error or a Stop ends the run
    private async Task RunRootAsync(TriggerContext context)
    {
        foreach (var block in _root.Blocks.OfType<ExecutableBlock>().ToArray())
            if (!await block.SafeRunAsync(context)) return;
    }



    // mode



    // makes it run its C# script (a new one the first time: an id is picked, the script manager writes the file)
    public void SwitchToCSharp()
    {
        if (IsCSharp) return;

        if (_scriptId == null)
        {
            _scriptId = Scripts?.NewScriptId() ?? NewScriptId();
            ScriptOwners[_scriptId] = this;
        }

        _mode = BindableMode.CSharp;
        ModeChanged?.Invoke();
        Changed?.Invoke();
    }



    // makes it run its blocks again (the script is kept, switching back runs it again)
    public void SwitchToBlocks()
    {
        if (!IsCSharp) return;

        _mode = BindableMode.Blocks;
        ModeChanged?.Invoke();
        Changed?.Invoke();
    }



    // its script's limits: each in seconds, 0 < limit <= a day, or null for none
    public bool TrySetScriptLimits(ScriptLimits limits)
    {
        foreach (var (name, seconds) in new[] { ("loop limit", limits.LoopSeconds), ("run limit", limits.RunSeconds), ("lock wait", limits.LockWaitSeconds) })
        {
            if (seconds is not { } value || value > 0 && value <= ScriptLimits.MaxSeconds) continue;
            StreamAssist.Components.OutputConsole.Model.AppLog.WriteError($"can't set {name} to {value} s (more than 0, up to a day, or off)");
            return false;
        }

        SetScriptLimits(limits);
        return true;
    }



    private void SetScriptLimits(ScriptLimits limits)
    {
        if (limits == _scriptLimits) return;

        _scriptLimits = limits;
        ScriptLimitsChanged?.Invoke();
        Changed?.Invoke();
    }



    // every script id a binding has right now (the script manager sweeps files no binding has)
    public static IReadOnlyCollection<string> LiveScriptIds => ScriptOwners.Keys;



    // 12 lowercase letters and digits, starting with a letter (it's part of the script's namespace)
    public static string NewScriptId()
    {
        const string letters = "abcdefghijklmnopqrstuvwxyz";
        const string chars = letters + "0123456789";
        string id;
        do
        {
            var picked = new char[12];
            picked[0] = letters[System.Security.Cryptography.RandomNumberGenerator.GetInt32(letters.Length)];
            for (var i = 1; i < picked.Length; i++) picked[i] = chars[System.Security.Cryptography.RandomNumberGenerator.GetInt32(chars.Length)];
            id = new string(picked);
        } while (ScriptOwners.ContainsKey(id));

        return id;
    }



    // loaded: a script id another live binding already has means this is a copy of it, which gets a file of its own
    void IJsonOnDeserialized.OnDeserialized()
    {
        if (_scriptId == null) return;

        if (ScriptOwners.TryGetValue(_scriptId, out var owner) && owner != this)
        {
            var copyId = Scripts?.NewScriptId() ?? NewScriptId();
            Scripts?.CopyScript(_scriptId, copyId);
            _scriptId = copyId;
        }

        ScriptOwners[_scriptId] = this;
    }



    // label



    // only something with blocks or a script can be labelled (nothing bound, nothing to label)
    public void SetLabel(string label)
    {
        if (label == _label) return;
        if (!HasContent && label != "") return;

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
        if (HasContent) return;

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

        if (_scriptId != null && ScriptOwners.TryGetValue(_scriptId, out var owner) && owner == this) ScriptOwners.Remove(_scriptId);

        _root.Dispose();
    }
}
