using System;
using System.Text.Json.Serialization;
using StreamAssist.Components.AppAction.Model;
using StreamAssist.Components.Main.Model;

namespace StreamAssist.Components.Keyboard.Model;



public class KeybindModel
{
    // private



    private bool _isEnabled;
    private ActionGroupModel _onPressed;
    private ActionGroupModel _onReleased;
    private ActionGroupModel _onRepeated;



    // public



    [JsonIgnore] public bool IsEnabled => _isEnabled;
    [JsonIgnore] public ActionGroupModel OnPressed => _onPressed;
    [JsonIgnore] public ActionGroupModel OnReleased => _onReleased;
    [JsonIgnore] public ActionGroupModel OnRepeated => _onRepeated;



    // json



    [JsonInclude] [JsonPropertyName("isEnabled")]
    private bool JsonIsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value;
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



    public event Action<bool>? IsEnabledChanged;


    
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



    // setters



    public bool TrySetIsEnabled(bool isEnabled)
    {
        if (isEnabled == _isEnabled)
        {
            AppLog.WriteError($"Could not set isEnabled - isEnabled is already {isEnabled}");
            return false;
        }

        SetIsEnabled(isEnabled);
        return true;
    }



    private void SetIsEnabled(bool isEnabled)
    {
        _isEnabled = isEnabled;
        IsEnabledChanged?.Invoke(_isEnabled);
    }
}