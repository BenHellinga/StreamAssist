using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Core.Dragging;

namespace StreamAssist.Components.Blocks.UI;



// copy and paste of blocks, whole bindings and other things (a component adds its own kind, e.g. an input device box),
// through the system clipboard as JSON text: {"streamAssist": "<kind>", "data": ...}, so it survives a restart and can
// be kept in a text file. A paste is a drop of a fresh copy: what's on the clipboard becomes the drag payload its kind
// uses (a block: BlockDragPayload, a binding: BindableDragPayload) and the target decides, as for a drag, whether it fits
// (a paste button only shows where it does) and places its own copy. Text that isn't ours pastes nowhere
public static class BlockClipboard
{
    // constants



    private const string KindProperty = "streamAssist";
    private const string DataProperty = "data";
    public const string BlockKind = "block";
    public const string BindingKind = "binding";



    // private



    // kind -> its payload from the copied data (null if it can't be read)
    private static readonly Dictionary<string, Func<JsonElement, object?>> PayloadReaders = new()
    {
        [BlockKind] = data => data.Deserialize<PendingBlockData>() is { } pending && BlockResolver.Resolve(pending) is { } block
            ? new BlockDragPayload(block) : null,
        [BindingKind] = data => data.Deserialize<BindableModel>() is { } bindable ? new BindableDragPayload(bindable) : null,
    };

    private static string? _text; // what's on the clipboard, ours or not
    private static object? _payload; // read from it the first time it's needed
    private static bool _payloadRead;



    // public



    // the system clipboard (set by the main window)
    public static Func<string, Task>? WriteText { get; set; }
    public static Func<Task<string?>>? ReadText { get; set; }



    // events



    public static event Action? Changed; // something else is on the clipboard (paste buttons check again)



    // public api



    // a component's own kind of copy (e.g. the Input tab's input device boxes): read gives its payload, which its
    // drop targets accept
    public static void RegisterKind(string kind, Func<JsonElement, object?> read)
    {
        PayloadReaders[kind] = read;
        if (_payload == null) _payloadRead = false; // read again, it may be this kind
    }



    public static void CopyBlock(BlockModel block) => Copy(BlockKind, BlockResolver.GetData(block));
    public static void CopyBindable(BindableModel bindable) => Copy(BindingKind, bindable);



    public static void Copy(string kind, object data)
    {
        var text = JsonSerializer.Serialize(new Dictionary<string, object> { [KindProperty] = kind, [DataProperty] = data });
        SetText(text);
        _ = WriteSafelyAsync(text);
    }



    // reads the system clipboard again (when the app gets focus: something may have been copied elsewhere)
    public static async Task RefreshAsync()
    {
        if (ReadText == null) return;

        try
        {
            SetText(await ReadText());
        }
        catch (Exception)
        {
            // the clipboard can be busy (another app has it open): keep what we had
        }
    }



    // whether pasting here would be taken, the way a drop of the same thing would
    public static bool CanPasteOn(IDroppable target) => Payload is { } payload && target.CanAcceptDropPayload(payload);



    // drops a fresh copy of what's on the clipboard on the target (it places its own copy, as for a drag)
    public static bool TryPasteOn(IDroppable target, object? zoneContext)
    {
        if (Payload is not { } payload || !target.CanAcceptDropPayload(payload)) return false;

        target.OnDroppedOn(payload, zoneContext);
        return true;
    }



    // helpers



    private static object? Payload
    {
        get
        {
            if (_payloadRead) return _payload;

            _payload = Read(_text);
            _payloadRead = true;
            return _payload;
        }
    }



    private static void SetText(string? text)
    {
        if (text == _text) return;

        DisposePayload();
        _text = text;
        Changed?.Invoke();
    }



    private static object? Read(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || !text.TrimStart().StartsWith('{')) return null;

        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty(KindProperty, out var kind) || kind.ValueKind != JsonValueKind.String
                || !root.TryGetProperty(DataProperty, out var data)
                || !PayloadReaders.TryGetValue(kind.GetString()!, out var read)) return null;

            return read(data.Clone());
        }
        catch (Exception)
        {
            return null; // not ours, or not something this version can read
        }
    }



    private static void DisposePayload()
    {
        switch (_payload)
        {
            case BlockDragPayload block: block.Block.Dispose(); break;
            case BindableDragPayload bindable: bindable.Bindable.Dispose(); break;
            case IDisposable disposable: disposable.Dispose(); break;
        }

        _payload = null;
        _payloadRead = false;
    }



    private static async Task WriteSafelyAsync(string text)
    {
        if (WriteText == null) return;

        try
        {
            await WriteText(text);
        }
        catch (Exception)
        {
            // the clipboard was busy: the copy still pastes inside the app (it's kept here)
        }
    }
}
