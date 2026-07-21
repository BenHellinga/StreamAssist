using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using StreamAssist.Components.Core;
using StreamAssist.Components.Main.Model;

namespace StreamAssist.Components.Keyboard.Model;



// contains data for a single keybind config
public class KeybindConfigModel : ModelBase, IJsonOnDeserialized
{
    // private



    private string _name;
    private Dictionary<ushort, KeybindModel> _keybinds;



    // public



    [JsonIgnore] public string Name => _name;
    [JsonIgnore] public IReadOnlyDictionary<ushort, KeybindModel> Keybinds => _keybinds;



    // json



    [JsonInclude] [JsonPropertyName("name")]
    private string JsonName
    {
        get => _name;
        set => _name = value;
    }

    [JsonInclude] [JsonPropertyName("keybinds")]
    private Dictionary<ushort, KeybindModel> JsonKeybinds
    {
        get => _keybinds;
        set => _keybinds = value;
    }



    // events



    public event Action<ushort, KeybindModel>? KeyBound;
    public event Action<ushort, KeybindModel>? KeyUnbound;



    // constructors



    public KeybindConfigModel()
    {
        _name = "null";
        _keybinds = [];
    }



    public KeybindConfigModel(string name, Dictionary<ushort, KeybindModel>? keybinds)
    {
        _name = name;
        _keybinds = keybinds ?? [];

        foreach (var (vk, keybind) in _keybinds)
            keybind.AttachUnbindSelf(() => UnbindKey(vk));
    }



    // deserialized



    // json deserialization is done in reverse, so this has to be done after all keybinds are already made
    void IJsonOnDeserialized.OnDeserialized()
    {
        foreach (var (vk, keybind) in _keybinds)
            keybind.AttachUnbindSelf(() => UnbindKey(vk));
    }



    // keybind management



    public bool TryBindKey(ushort vk, KeybindModel keybind)
    {
        if (_keybinds.ContainsKey(vk))
        {
            AppLog.WriteError("Could not bind key - key already bound");
            return false;
        }

        BindKey(vk, keybind);
        return true;
    }



    private void BindKey(ushort vk, KeybindModel keybind)
    {
        keybind.AttachUnbindSelf(() => UnbindKey(vk));
        _keybinds.Add(vk, keybind);
        KeyBound?.Invoke(vk, keybind);
    }



    public void UnbindKey(ushort key)
    {
        if (!_keybinds.Remove(key, out var keybind)) return;
        KeyUnbound?.Invoke(key, keybind!);
        keybind.Dispose();
    }



    // disposal



    public override void Dispose()
    {
        foreach (var keybind in _keybinds.Values)
            keybind.Dispose();
    }
}