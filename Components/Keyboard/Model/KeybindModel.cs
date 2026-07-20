using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using StreamAssist.Components.AppAction.Model;
using StreamAssist.Components.Core;

namespace StreamAssist.Components.Keyboard.Model;



// contains data for a single keybind
public class KeybindModel : ModelBase
{
    // private



    private bool _isDisabled;
    private ActionGroupModel _onPressed;
    private ActionGroupModel _onReleased;
    private ActionGroupModel _onRepeated;



    // public



    [JsonIgnore] public bool IsDisabled => _isDisabled;
    [JsonIgnore] public ActionGroupModel OnPressed => _onPressed;
    [JsonIgnore] public ActionGroupModel OnReleased => _onReleased;
    [JsonIgnore] public ActionGroupModel OnRepeated => _onRepeated;



    // json



    [JsonInclude] [JsonPropertyName("isDisabled")]
    private bool JsonIsDisabled
    {
        get => _isDisabled;
        set => _isDisabled = value;
    }

    [JsonInclude] [JsonPropertyName("onPressed")]
    private ActionGroupModel JsonOnPressed
    {
        get => _onPressed;
        set => _onPressed = value;
    }

    [JsonInclude] [JsonPropertyName("onReleased")]
    private ActionGroupModel JsonOnReleased
    {
        get => _onReleased;
        set => _onReleased = value;
    }

    [JsonInclude] [JsonPropertyName("onRepeated")]
    private ActionGroupModel JsonOnRepeated
    {
        get => _onRepeated;
        set => _onRepeated = value;
    }



    // events



    public event Action<bool>? IsDisabledChanged;


    
    // constructors



    public KeybindModel()
    {
        _isDisabled = false;
        _onPressed = new();
        _onReleased = new();
        _onRepeated = new();
    }



    // isDisabled management



    public void SetIsDisabled(bool isDisabled)
    {
        if (isDisabled == _isDisabled) return;
        _isDisabled = isDisabled;
        IsDisabledChanged?.Invoke(_isDisabled);
    }



    // public api



    public void AttachUnbindSelf(Action? tryUnbindSelf)
    {
        _onPressed.SetUnbindSelf(tryUnbindSelf);
        _onReleased.SetUnbindSelf(tryUnbindSelf);
        _onRepeated.SetUnbindSelf(tryUnbindSelf);
    }



    public static KeybindModel DeepCopy(KeybindModel keybind)
    {
        var json = JsonSerializer.Serialize(keybind);
        return JsonSerializer.Deserialize<KeybindModel>(json)!;
    }



    // disposal



    public override void Dispose()
    {
        _onPressed.Dispose();
        _onReleased.Dispose();
        _onRepeated.Dispose();
    }
}