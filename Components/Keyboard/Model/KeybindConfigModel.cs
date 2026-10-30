using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Core.Mvvm;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Keyboard.Model;



// contains data for a single keybind config (which key runs which bindable)
public class KeybindConfigModel : ModelBase, IJsonOnDeserialized
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

        AttachKeybinds();
    }



    // deserialized



    // the dictionary is filled in by the json loader, so link each bindable back to this config afterwards
    void IJsonOnDeserialized.OnDeserialized() => AttachKeybinds();



    // keybind management



    public bool TryBindKey(ushort vk, BindableModel keybind)
    {
        if (_keybinds.ContainsKey(vk))
        {
            AppLog.WriteError("can't bind key, already bound");
            return false;
        }

        BindKey(vk, keybind);
        return true;
    }



    private void BindKey(ushort vk, BindableModel keybind)
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



    // helpers



    // empty bindables (saved before they removed themselves) are dropped, the rest can remove themselves from now on
    private void AttachKeybinds()
    {
        foreach (var (vk, keybind) in _keybinds.ToArray())
        {
            if (!keybind.HasBlock)
            {
                _keybinds.Remove(vk);
                keybind.Dispose();
                continue;
            }

            keybind.AttachUnbindSelf(() => UnbindKey(vk));
        }
    }



    // disposal



    public override void Dispose()
    {
        foreach (var keybind in _keybinds.Values)
            keybind.Dispose();
    }
}
