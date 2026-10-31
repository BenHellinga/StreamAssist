using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Blocks.Model;



// a setting that's either typed in, or comes from a value block placed in it (the block wins, a disabled block falls
// back to the typed text). Typed text has {name} filled in with parameters first, then is read as the slot's kind,
// so an int slot can be "{viewers}". Only blocks whose kind converts automatically fit (see ValueTypes)
public abstract class ValueSlot : BlockSlotBase
{
    // private



    private readonly ValueKind? _kind;
    private readonly BlockSlot _block;
    private string _text;
    private string _connector;



    // public



    public ValueKind? Kind => _kind; // null = any kind (e.g. Equals)
    public BlockSlot BlockSlot => _block;
    public string Text => _text;
    public string Connector => _connector; // shown on its own line above the slot, e.g. Equals' "==" (display only, not saved)

    public override IReadOnlyList<BlockModel> Blocks => [];
    public override IReadOnlyList<BlockSlotBase> InnerSlots => [_block];



    // events



    public event Action? TextChanged;
    public event Action? ConnectorChanged;



    // constructor



    protected ValueSlot(string label, ValueKind? kind) : base(label, typeof(ValueBlock))
    {
        _kind = kind;
        _text = "";
        _connector = "";

        Filter = FitsKind;
        WantedKind = kind;
        _block = new(label, typeof(ValueBlock)) { Filter = FitsKind, WantedKind = kind };
        _block.Changed += RaiseChanged;
    }



    // public api



    public void SetText(string text)
    {
        if (text == _text) return;

        _text = text;
        TextChanged?.Invoke();
        RaiseChanged();
    }



    public void SetConnector(string connector)
    {
        if (connector == _connector) return;

        _connector = connector;
        ConnectorChanged?.Invoke();
    }



    public override void RemoveBlock(BlockModel block) => _block.RemoveBlock(block);



    // protected



    // the placed block's value and kind, null if there's no (enabled) block
    protected async Task<(ValueKind Kind, object Value)?> EvaluateBlockAsync(TriggerContext context)
    {
        if (_block.Block is not ValueBlock block || block.IsDisabled) return null;
        return (block.Kind, await block.EvaluateObjectAsync(context));
    }



    // logs why the value can't be read (naming the block and this slot) and returns the exception to throw
    protected BlockFailedException Fail(string reason)
    {
        var slot = Label == "" ? "" : $"{Label} ";
        AppLog.WriteError($"{Owner?.Name ?? "block"}: {slot}{reason}");
        return new BlockFailedException(reason);
    }



    // helpers



    private bool FitsKind(BlockModel block) =>
        block is ValueBlock value && (_kind == null || ValueTypes.CanConvert(value.Kind, _kind.Value));



    // json (owners save the slot through these)



    public ValueSlotData GetData() => new() { Text = _text, Block = _block.GetData() };



    public void LoadData(ValueSlotData? data)
    {
        if (data == null) return;

        _text = data.Text;
        _block.LoadData(data.Block);
    }



    // disposal



    public override void Dispose()
    {
        _block.Changed -= RaiseChanged;
        _block.Dispose();
    }
}



// a value slot of one kind: string, int, double, bool or char
public sealed class ValueSlot<T> : ValueSlot where T : notnull
{
    public ValueSlot(string label) : base(label, ValueTypes.KindOf(typeof(T))) { }



    public async Task<T> EvaluateAsync(TriggerContext context)
    {
        var kind = Kind!.Value;

        if (await EvaluateBlockAsync(context) is { } fromBlock)
            return (T)ValueTypes.Convert(fromBlock.Value, fromBlock.Kind, kind);

        var text = context.Format(Text);
        if (ValueTypes.TryParse(text, kind, out var value)) return (T)value;

        throw Fail(text.Trim() == ""
            ? $"empty, needs {ValueTypes.Describe(kind)}"
            : $"'{text}' is not {ValueTypes.Describe(kind)}");
    }
}



// a value slot that takes any kind (e.g. Equals): a block gives its value and kind, typed text has no kind of its own
// (the block using it decides, e.g. Equals reads it as the kind of the other side)
public sealed class AnyValueSlot : ValueSlot
{
    public AnyValueSlot(string label) : base(label, null) { }



    // Kind is null for typed text (with {name} filled in)
    public async Task<(ValueKind? Kind, object Value)> EvaluateRawAsync(TriggerContext context)
    {
        if (await EvaluateBlockAsync(context) is { } fromBlock) return (fromBlock.Kind, fromBlock.Value);
        return (null, context.Format(Text));
    }
}



// how a value slot is saved
public class ValueSlotData
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("block")]
    public PendingBlockData? Block { get; set; }
}
