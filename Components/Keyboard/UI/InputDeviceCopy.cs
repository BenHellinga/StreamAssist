using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Keyboard.UI;



// a copied input device box (BlockClipboard kind "inputDevice"): its layout, whether it's off, and its keys' bindings.
// It's also the paste's payload: the keyboard list takes it on its ghost slot and makes a new box like it
// (KeyboardListModel.TryCreateSlotCopy). The device isn't copied, the new box picks its own
public sealed class InputDeviceCopy : IDisposable
{
    public const string Kind = "inputDevice";

    [JsonPropertyName("layout")] public string Layout { get; set; } = "";
    [JsonPropertyName("isDisabled")] public bool IsDisabled { get; set; }
    [JsonPropertyName("keys")] public Dictionary<ushort, BindableModel> Keys { get; set; } = [];

    public InputDeviceCopy() { }

    public InputDeviceCopy(string layout, bool isDisabled, IReadOnlyDictionary<ushort, BindableModel>? keys)
    {
        Layout = layout;
        IsDisabled = isDisabled;
        Keys = keys?.ToDictionary(k => k.Key, k => k.Value) ?? []; // the same bindings, only written out
    }

    // a pasted copy owns the bindings it read (a copy being written out doesn't, it's never disposed)
    public void Dispose()
    {
        foreach (var bindable in Keys.Values) bindable.Dispose();
    }
}
