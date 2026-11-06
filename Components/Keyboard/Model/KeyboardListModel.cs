using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Core.Mvvm;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Keyboard.Model;



// contains the keyboard slots and the tab's configs: one config is in use for the whole tab, and holds every slot's keys
// (with no config, a slot has no keys: nothing runs and keys can't be bound)
public class KeyboardListModel : ModelBase, IJsonOnDeserialized
{
    // constants



    public const string NoneDevice = "None"; // reserved for ui



    // private



    private IReadOnlyList<InputDeviceModel>? _devices;
    private readonly ConfigSet<KeyboardConfigModel> _configs;
    private List<KeyboardSlotModel> _slots;



    // public



    [JsonIgnore] public ConfigSet<KeyboardConfigModel> Configs => _configs;
    [JsonIgnore] public IReadOnlyList<KeyboardSlotModel> Slots => _slots;
    [JsonIgnore] public IReadOnlyList<InputDeviceModel>? Devices => _devices;



    // json



    [JsonInclude] [JsonPropertyName("configs")]
    private List<KeyboardConfigModel> JsonConfigs
    {
        get => _configs.Items.ToList();
        set => _configs.LoadItems(value);
    }

    [JsonInclude] [JsonPropertyName("config")]
    private string? JsonConfig
    {
        get => _configs.SelectedName;
        set => _configs.LoadSelected(value);
    }

    [JsonInclude] [JsonPropertyName("slots")]
    private List<KeyboardSlotModel> JsonSlots
    {
        get => _slots;
        set => _slots = value;
    }



    // events



    public event Action<KeyboardSlotModel, int>? SlotCreated; // the slot, where it went
    public event Action<KeyboardSlotModel, int, int>? SlotMoved; // the slot, where it was, where it is now
    public event Action<KeyboardSlotModel>? SlotDeleted;
    public event Action? DevicesChanged; // the keyboards plugged in changed (each slot has looked for its own again)
    public event Action? Changed; // the configs' contents, the config in use, or which configs there are changed (saved)



    // constructor



    public KeyboardListModel()
    {
        _configs = new("keyboard config", () => new KeyboardConfigModel());
        _slots = [];
        _devices = null; // set later through AttachDevices

        _configs.SelectedChanged += OnSelectedConfigChanged;
        _configs.Changed += OnConfigsChanged;
    }



    // deserialized



    // json deserialization is done in reverse, so this has to be done after all slots are already made: slots without an
    // id (or sharing one) get a new one, then each slot shows its keys in the config in use
    void IJsonOnDeserialized.OnDeserialized()
    {
        var used = new HashSet<int>();
        foreach (var slot in _slots)
            if (slot.Id <= 0 || !used.Add(slot.Id))
            {
                slot.SetId(NextSlotId());
                used.Add(slot.Id);
            }

        ShowSelectedConfig();
    }



    // attach devices



    // the keyboards plugged in: at startup (after the list and slots are loaded), and again whenever one is plugged in
    // or removed (KeyboardComponentManager passes KeyboardInputManager's list)
    public void AttachDevices(IReadOnlyList<InputDeviceModel> devices)
    {
        var changed = _devices != null;
        _devices = devices;

        foreach (var slot in _slots)
            slot.AttachDevices(_devices);

        if (changed) DevicesChanged?.Invoke();
    }



    // slot management



    // a keyboard slot at the end
    public void CreateSlot(out KeyboardSlotModel slot) => TryCreateSlot(KeyboardSlotModel.KeyboardLayout, _slots.Count, out slot);



    // a slot of this layout (the Keyboard tab's input device cards: Keyboard, Generic) before the one now at index
    // (the list's length = at the end)
    public bool TryCreateSlot(string layout, int index, out KeyboardSlotModel slot)
    {
        slot = null!;
        if (!KeyboardSlotModel.Layouts.Contains(layout))
        {
            AppLog.WriteError($"no keyboard layout '{layout}'");
            return false;
        }

        slot = new KeyboardSlotModel(_devices, NextSlotId(), layout);
        slot.ShowConfig(_configs.Selected?.ForSlot(slot.Id));

        index = Math.Clamp(index, 0, _slots.Count);
        _slots.Insert(index, slot);
        SlotCreated?.Invoke(slot, index);
        return true;
    }



    // a slot to just before the one now at index (the list's length = last), e.g. dragged there to reorder
    public bool TryMoveSlot(KeyboardSlotModel slot, int index)
    {
        var from = _slots.IndexOf(slot);
        if (from < 0)
        {
            AppLog.WriteError("can't move keyboard, not in the list");
            return false;
        }

        var to = Math.Clamp(index > from ? index - 1 : index, 0, _slots.Count - 1);
        if (to == from) return true;

        _slots.RemoveAt(from);
        _slots.Insert(to, slot);
        SlotMoved?.Invoke(slot, from, to);
        return true;
    }



    // its keys go with it, in every config
    public bool TryDeleteSlot(KeyboardSlotModel slot)
    {
        if (!_slots.Contains(slot)) return true; // already gone, nothing to say

        DeleteSlot(slot);
        return true;
    }



    private void DeleteSlot(KeyboardSlotModel slot)
    {
        _slots.Remove(slot);
        SlotDeleted?.Invoke(slot);
        slot.ShowConfig(null);
        slot.Dispose();

        foreach (var config in _configs.Items)
            config.RemoveSlot(slot.Id);
    }



    // model events



    private void OnSelectedConfigChanged(KeyboardConfigModel? _, KeyboardConfigModel? __) => ShowSelectedConfig();



    private void OnConfigsChanged() => Changed?.Invoke();



    // helpers



    private void ShowSelectedConfig()
    {
        foreach (var slot in _slots)
            slot.ShowConfig(_configs.Selected?.ForSlot(slot.Id));
    }



    private int NextSlotId() => _slots.Count == 0 ? 1 : _slots.Max(s => s.Id) + 1;



    // disposal



    public override void Dispose()
    {
        _configs.SelectedChanged -= OnSelectedConfigChanged;
        _configs.Changed -= OnConfigsChanged;

        foreach (var slot in _slots)
            slot.Dispose();

        _configs.Dispose();
    }
}
