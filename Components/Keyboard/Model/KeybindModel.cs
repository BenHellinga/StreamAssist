using System.Text.Json.Serialization;
using StreamAssist.Components.AppAction.Model;
using StreamAssist.Components.Main;

namespace StreamAssist.Components.Keyboard.Model;



public class KeybindModel
{
    // private storage fields
    private ActionGroupModel _onPressed;
    private ActionGroupModel _onReleased;
    private ActionGroupModel _onRepeated;

    // public getter properties
    [JsonIgnore] public ActionGroupModel OnPressed => _onPressed;
    [JsonIgnore] public ActionGroupModel OnReleased => _onReleased;
    [JsonIgnore] public ActionGroupModel OnRepeated => _onRepeated;



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

    [JsonInclude] [JsonPropertyName("onRepeated")]
    private ActionGroupModel JsonOnRepeated
    {
        get => _onRepeated;
        set => _onRepeated = value;
    }



    // constructors



    public KeybindModel()
    {
        _onPressed = new();
        _onReleased = new();
        _onRepeated = new();
    }



    public KeybindModel(ActionGroupModel? onPressed = null, ActionGroupModel? onReleased = null, ActionGroupModel? onRepeated = null)
    {
        _onPressed = onPressed ?? new();
        _onReleased = onReleased ?? new();
        _onRepeated = onRepeated ?? new();
    }
}