using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using StreamAssist.Components.AppAction.Model;
using StreamAssist.Components.Main.Model;

namespace StreamAssist.Components.Keyboard.Model;



public class KeybindConfigModel
{
    // private storage fields
    private string _name;
    private Dictionary<ushort, KeybindModel> _keybinds;

    // public getter properties
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
        set
        {
            _keybinds = value;
            foreach (var (vk, keybind) in _keybinds)
                keybind.SetSelfUnbindLambda(MakeSelfUnbindLambda(vk, keybind));
        }
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



    public KeybindConfigModel(string name, Dictionary<ushort, KeybindModel>? keybinds = null)
    {
        _name = name;
        _keybinds = keybinds ?? [];
    }



    // keybind management



    public void TryBindKey(ushort vk, KeybindModel keybind)
    {
        if (_keybinds.ContainsKey(vk))
        {
            AppLog.WriteError("Could not bind key - key already bound");
            return;
        }

        BindKey(vk, keybind);
    }



    private void BindKey(ushort vk, KeybindModel keybind)
    {
        keybind.SetSelfUnbindLambda(MakeSelfUnbindLambda(vk, keybind));
        _keybinds.Add(vk, keybind);
        KeyBound?.Invoke(vk, keybind);
    }



    public bool TryUnbindKey(ushort key)
    {
        if (!_keybinds.ContainsKey(key))
        {
            AppLog.WriteError("Could not unbind key - key not bound");
            return false;
        }

        UnbindKey(key);
        return true;
    }



    private void UnbindKey(ushort key)
    {
        _keybinds.Remove(key, out var bindingModel);
        KeyUnbound?.Invoke(key, bindingModel!);
    }



    // helpers



    private Action MakeSelfUnbindLambda(ushort vk, KeybindModel keybind)
    {
        return () =>
        {
            if (0 == keybind.OnPressed.Actions.Count +
                     keybind.OnReleased.Actions.Count +
                     keybind.OnRepeated.Actions.Count)
                TryUnbindKey(vk);
        };
    }
}