using System;
using System.Collections.Generic;
using System.Linq;
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
        _block = new(label, typeof(ValueBlock)) { Filter = FitsKind, WantedKind = kind, ValueSlot = this };
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



    // the typed text with each {name} going through fill (see TriggerContext.Format), or a placed block's value as text
    public async Task<string> EvaluateTextAsync(TriggerContext context, Func<string, string, int, string> fill)
    {
        if (await EvaluateBlockAsync(context) is { } fromBlock) return (string)ValueTypes.Convert(fromBlock.Value, fromBlock.Kind, ValueKind.Text);
        return context.Format(Text, fill);
    }



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



    // a block that only picks a dropdown's option (Random Option) fits a dropdown, nowhere else
    private bool FitsKind(BlockModel block) =>
        block is ValueBlock value && (_kind == null || ValueTypes.CanConvert(value.Kind, _kind.Value))
        && (block is not IMenuOptionBlock || this is MenuSlot);



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



// a dropdown setting (e.g. On / Off / Toggle): one of its options picked, or a value block placed in it giving the
// option as text (case ignored, e.g. a variable holding "Toggle", or Random Option). Its options are asked for each
// time they're needed, so a list that changes (audio devices, Voicemeeter channels, variables) is always current. A
// fixed list refuses anything else when it runs; a changing one passes it on (the block says if it isn't there)
public sealed class MenuSlot : ValueSlot
{
    private readonly Func<List<string>> _options;
    private readonly bool _isFixed;



    public MenuSlot(string label, Func<List<string>> options, string option, bool isFixed = true) : base(label, ValueKind.Text)
    {
        _options = options;
        _isFixed = isFixed;
        SetText(option);
    }



    // the options right now (none if they can't be listed, e.g. Voicemeeter isn't running)
    public List<string> Options
    {
        get
        {
            try
            {
                return _options();
            }
            catch (Exception)
            {
                return [];
            }
        }
    }



    // what Random Option picks from: every option but a "nothing in particular" one (Default, None)
    public List<string> RandomOptions => Options.Where(o => o is not ("Default" or "None")).ToList();



    // the picked option, spelled as in the list (a fixed list refuses anything else)
    public async Task<string> EvaluateAsync(TriggerContext context)
    {
        var value = (await EvaluateBlockAsync(context) is { } fromBlock
            ? (string)ValueTypes.Convert(fromBlock.Value, fromBlock.Kind, ValueKind.Text)
            : Text).Trim();

        var options = Options;
        if (options.FirstOrDefault(o => string.Equals(o, value, StringComparison.OrdinalIgnoreCase)) is { } option) return option;
        if (!_isFixed) return value;

        throw Fail(value == "" ? "nothing picked" : $"'{value}' isn't one of its options ({string.Join(", ", options)})");
    }
}



// a value block that only fits a dropdown (MenuSlot), e.g. Random Option
public interface IMenuOptionBlock;



// how a value slot is saved
public class ValueSlotData
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("block")]
    public PendingBlockData? Block { get; set; }
}
