using System;
using System.Dynamic;
using System.Text.Json.Serialization;
using StreamAssist.Components.AppAction.Model;
using StreamAssist.Components.Main.Model;

namespace StreamAssist.Components.Keyboard.Model;



public class KeybindModel
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
        _onPressed = new();
        _onReleased = new();
        _onRepeated = new();

        _isDisabled = false;
    }



    // setters



    public void SetIsDisabled(bool isDisabled)
    {
        if (isDisabled == _isDisabled) return;
        _isDisabled = isDisabled;
        IsDisabledChanged?.Invoke(_isDisabled);
    }



    public void SetSelfUnbindLambda(Action? tryUnbindSelf)
    {
        _onPressed.SetUnbindSelfLambda(tryUnbindSelf);
        _onReleased.SetUnbindSelfLambda(tryUnbindSelf);
        _onRepeated.SetUnbindSelfLambda(tryUnbindSelf);
    }
}