using System.Collections.Generic;
using System.Text.Json.Serialization;
using StreamAssist.Components.AppAction.Model;

namespace StreamAssist.Components.Keyboard.Model;



public class KeyboardBindingModel
{
    // private storage fields
    private ActionGroupModel _onPressed;
    private ActionGroupModel _onReleased;

    // public getter properties
    [JsonIgnore] public ActionGroupModel OnPressed => _onPressed;
    [JsonIgnore] public ActionGroupModel OnReleased => _onReleased;



    // json



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



    // constructors



    public KeyboardBindingModel()
    {
        _onPressed = new();
        _onReleased = new();
    }



    public KeyboardBindingModel(ActionGroupModel? onPressed = null, ActionGroupModel? onReleased = null)
    {
        _onPressed = onPressed ?? new();
        _onReleased = onReleased ?? new();
    }
}