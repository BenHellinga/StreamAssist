using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using StreamAssist.Core.Mvvm;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Keyboard.Model;



// contains data for a single keybind config (which key runs which bindable)
public class KeybindConfigModel : ModelBase
{
    // private



    private string _name;
    private Dictionary<ushort, BindableModel> _keybinds;



    // public



    [JsonIgnore] public string Name => _name;
    [JsonIgnore] public IReadOnlyDictionary<ushort, BindableModel> Keybinds => _keybinds;



    // json



    [JsonInclude] [JsonPropertyName("name")]
    private string JsonName
    {
        get => _name;
        set => _name = value;
    }

    [JsonInclude] [JsonPropertyName("bindables")]
    private Dictionary<ushort, BindableModel> JsonKeybinds
    {
        get => _keybinds;
        set => _keybinds = value;
    }



    // events



    public event Action<ushort, BindableModel>? KeyBound;
    public event Action<ushort, BindableModel>? KeyUnbound;



    // constructors



    public KeybindConfigModel()
    {
        _name = "null";
        _keybinds = [];
    }



    public KeybindConfigModel(string name, Dictionary<ushort, BindableModel>? keybinds)
    {
        _name = name;
        _keybinds = keybinds ?? [];
    }



    // keybind management



    public bool TryBindKey(ushort vk, BindableModel keybind)
    {
        if (_keybinds.ContainsKey(vk))
        {
            AppLog.WriteError("Could not bind key - key already bound");
            return false;
        }

        BindKey(vk, keybind);
        return true;
    }



    private void BindKey(ushort vk, BindableModel keybind)
    {
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
