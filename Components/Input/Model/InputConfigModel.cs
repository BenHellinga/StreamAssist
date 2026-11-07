using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Input.Model;



// one config of the Input tab: every keyboard slot's keys (by the slot's id). Switching configs swaps every
// keyboard's keys at once (see InputListModel.Configs). A slot's keys are made when it's first shown in this config
public sealed class InputConfigModel : INamedConfig
{
    // private



    private string _name;
    private readonly Dictionary<int, KeybindConfigModel> _slotKeybinds;



    // public



    [JsonIgnore] public string Name => _name;
    [JsonIgnore] public IReadOnlyDictionary<int, KeybindConfigModel> SlotKeybinds => _slotKeybinds;



    // json



    [JsonInclude] [JsonPropertyName("name")]
    private string JsonName
    {
        get => _name;
        set => _name = value;
    }

    // by slot id, each key code to its binding (boxes with no keys bound aren't saved)
    [JsonInclude] [JsonPropertyName("slotKeybinds")]
    private Dictionary<int, Dictionary<ushort, BindableModel>> JsonSlotKeybinds
    {
        get => _slotKeybinds.Where(k => k.Value.Keybinds.Count > 0).ToDictionary(k => k.Key, k => k.Value.Keybinds.ToDictionary());
        set
        {
            foreach (var (slotId, keybinds) in value)
                Add(slotId, new KeybindConfigModel(_name, keybinds));
        }
    }



    // events



    public event Action? Changed;



    // constructor



    public InputConfigModel()
    {
        _name = "";
        _slotKeybinds = [];
    }



    // public api



    public void SetName(string name)
    {
        if (name == _name) return;

        _name = name;
        Changed?.Invoke();
    }



    // a slot's keys in this config, empty ones made the first time they're asked for
    public KeybindConfigModel ForSlot(int slotId)
    {
        if (_slotKeybinds.TryGetValue(slotId, out var keys)) return keys;

        keys = new KeybindConfigModel(_name, null);
        Add(slotId, keys);
        return keys;
    }



    // a slot was deleted: its keys go with it
    public void RemoveSlot(int slotId)
    {
        if (!_slotKeybinds.Remove(slotId, out var keys)) return;

        UnsubscribeFromKeys(keys);
        keys.Dispose();
        Changed?.Invoke();
    }



    // model events



    private void OnKeyBound(ushort _, BindableModel bindable)
    {
        bindable.Changed += OnContentsChanged;
        Changed?.Invoke();
    }



    private void OnKeyUnbound(ushort _, BindableModel bindable)
    {
        bindable.Changed -= OnContentsChanged;
        Changed?.Invoke();
    }



    private void OnContentsChanged() => Changed?.Invoke();



    // helpers



    private void Add(int slotId, KeybindConfigModel keys)
    {
        if (_slotKeybinds.Remove(slotId, out var old))
        {
            UnsubscribeFromKeys(old);
            old.Dispose();
        }

        _slotKeybinds[slotId] = keys;
        SubscribeToKeys(keys);
    }



    // a bindable raises Changed for anything inside it, so one subscription covers its whole block tree
    private void SubscribeToKeys(KeybindConfigModel keys)
    {
        keys.KeyBound += OnKeyBound;
        keys.KeyUnbound += OnKeyUnbound;
        foreach (var bindable in keys.Keybinds.Values)
            bindable.Changed += OnContentsChanged;
    }



    private void UnsubscribeFromKeys(KeybindConfigModel keys)
    {
        keys.KeyBound -= OnKeyBound;
        keys.KeyUnbound -= OnKeyUnbound;
        foreach (var bindable in keys.Keybinds.Values)
            bindable.Changed -= OnContentsChanged;
    }



    // disposal



    public void Dispose()
    {
        foreach (var keys in _slotKeybinds.Values)
        {
            UnsubscribeFromKeys(keys);
            keys.Dispose();
        }

        _slotKeybinds.Clear();
    }
}
