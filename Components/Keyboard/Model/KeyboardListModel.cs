using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Components.StreamAction;

namespace StreamAssist.Components.Keyboard.Model;



// TODO it should make a slot if there are zero slots (constructor? something else?)
public class KeyboardListModel
{
    public const string NoneDevice = "None";
    public const string NoneConfig = "None";

    [JsonPropertyName("configs")]
    public List<KeyboardConfigModel> Configs { get; set; } = [];

    [JsonPropertyName("slots")]
    public List<KeyboardSlotModel> Slots { get; set; } = [];



    // events



    public event Action<KeyboardSlotModel>? SlotAdded;
    public event Action<KeyboardSlotModel>? SlotRemoved;
    public event Action? ConfigListUpdated;
    public event Action<string>? ConfigBindingsChanged;



    // slot management



    public void AddSlot()
    {
        var slot = new KeyboardSlotModel();
        Slots.Add(slot);
        SlotAdded?.Invoke(slot);
    }



    public bool RemoveLastSlot()
    {
        if (Slots.Count <= 1) return false;

        var slot = Slots[^1];
        Slots.RemoveAt(Slots.Count - 1);
        SlotRemoved?.Invoke(slot);
        return true;
    }



    // config management



    public KeyboardConfigModel? CreateConfig(string name, Dictionary<ushort, string>? bindings = null)
    {
        var trimmed = name.Trim();
        
        if (string.IsNullOrWhiteSpace(trimmed)) return null;
        if (trimmed == NoneConfig) return null;
        if (Configs.Any(c => c.Name == trimmed)) return null;

        var config = new KeyboardConfigModel { Name = trimmed, KeyBindings = bindings ?? [] };
        Configs.Add(config);

        ConfigListUpdated?.Invoke();
        return config;
    }



    public bool DeleteConfig(string name)
    {
        var config = Configs.FirstOrDefault(c => c.Name == name);
        if (config is null) return false;

        Configs.Remove(config);

        foreach (var slot in Slots)
            if (slot.SelectedConfig == name)
                slot.SetConfig(NoneConfig);

        ConfigListUpdated?.Invoke();
        return true;
    }



    public void SetConfigBinding(string configName, ushort vk, string? actionTypeName)
    {
        var config = Configs.FirstOrDefault(c => c.Name == configName);
        if (config is null) return;

        if (actionTypeName is null)
            config.KeyBindings.Remove(vk);
        else
            config.KeyBindings[vk] = actionTypeName;

        ConfigBindingsChanged?.Invoke(configName);
    }



    // helper



    public Dictionary<ushort, IStreamAction> GetConfigBindings(string configName)
    {
        var result = new Dictionary<ushort, IStreamAction>();
        var config = Configs.FirstOrDefault(c => c.Name == configName);
        if (config is null) return result;

        foreach (var (vk, typeName) in config.KeyBindings)
        {
            var action = ActionResolver.Resolve(typeName);
            if (action is not null)
                result[vk] = action;
        }

        return result;
    }
}