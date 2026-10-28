using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.VisualBasic;
using StreamAssist.Components.AppAction.Model;
using StreamAssist.Core.Mvvm;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Keyboard.Model;



// contains data for a single keybind
public class KeybindModel : ModelBase
{
    // private



    private bool _isDisabled;
    private ActionGroupModel? _onPressed;
    private ActionGroupModel? _onReleased;
    private ActionGroupModel? _onRepeated;
    private Action? _unbindSelf;



    // public



    [JsonIgnore] public bool IsDisabled => _isDisabled;
    [JsonIgnore] public ActionGroupModel? OnPressed => _onPressed;
    [JsonIgnore] public ActionGroupModel? OnReleased => _onReleased;
    [JsonIgnore] public ActionGroupModel? OnRepeated => _onRepeated;



    public enum TriggerType { OnPressed, OnReleased, OnRepeated }



    // json



    [JsonInclude] [JsonPropertyName("isDisabled")]
    private bool JsonIsDisabled
    {
        get => _isDisabled;
        set => _isDisabled = value;
    }

    [JsonInclude] [JsonPropertyName("onPressed")]
    private ActionGroupModel? JsonOnPressed
    {
        get => _onPressed;
        set
        {
            _onPressed = value;
            _onPressed?.AttachUnbindSelf(() => UnbindTrigger(TriggerType.OnPressed));
        }
    }

    [JsonInclude] [JsonPropertyName("onReleased")]
    private ActionGroupModel? JsonOnReleased
    {
        get => _onReleased;
        set
        {
            _onReleased = value;
            _onReleased?.AttachUnbindSelf(() => UnbindTrigger(TriggerType.OnReleased));
        }
    }

    [JsonInclude] [JsonPropertyName("onRepeated")]
    private ActionGroupModel? JsonOnRepeated
    {
        get => _onRepeated;
        set
        {
            _onRepeated = value;
            _onRepeated?.AttachUnbindSelf(() => UnbindTrigger(TriggerType.OnRepeated));
        }
    }



    // events



    public event Action<bool>? IsDisabledChanged;
    public event Action<TriggerType, ActionGroupModel>? TriggerBound;
    public event Action<TriggerType, ActionGroupModel>? TriggerUnbound;
    public event Action? FirstTriggerBound;


    
    // constructors



    public KeybindModel()
    {
        _isDisabled = false;
        _onPressed = null;
        _onReleased = null;
        _onRepeated = null;
    }



    // isDisabled management



    public void SetIsDisabled(bool isDisabled)
    {
        if (isDisabled == _isDisabled) return;
        _isDisabled = isDisabled;
        IsDisabledChanged?.Invoke(_isDisabled);
    }



    // action group management



    public bool TryBindTrigger(TriggerType trigger, ActionGroupModel group)
    {
        ref var triggerGroup = ref GetTriggerGroup(trigger);

        if (triggerGroup != null)
        {
            AppLog.WriteError($"Could not bind trigger '{trigger}' - trigger already bound");
            return false;
        }

        BindTrigger(trigger, group);
        return true;
    }



    private void BindTrigger(TriggerType trigger, ActionGroupModel group)
    {
        var wasEmpty = KeybindIsEmpty();

        ref var triggerGroup = ref GetTriggerGroup(trigger);
        triggerGroup = group;
        triggerGroup.AttachUnbindSelf(() => UnbindTrigger(trigger));
        TriggerBound?.Invoke(trigger, triggerGroup);

        if (wasEmpty) FirstTriggerBound?.Invoke();
    }



    public void UnbindTrigger(TriggerType trigger)
    {
        ref var triggerGroup = ref GetTriggerGroup(trigger);
        if (triggerGroup == null) return;

        var group = triggerGroup;
        triggerGroup = null;

        if (KeybindIsEmpty()) _unbindSelf?.Invoke();

        TriggerUnbound?.Invoke(trigger, group);
        group.Dispose();
    }



    // public api



    public void AttachUnbindSelf(Action? unbindSelf) => _unbindSelf = unbindSelf;



    public static KeybindModel DeepCopy(KeybindModel keybind)
    {
        var json = JsonSerializer.Serialize(keybind);
        return JsonSerializer.Deserialize<KeybindModel>(json)!;
    }



    // helpers



    private ref ActionGroupModel? GetTriggerGroup(TriggerType trigger)
    {
        switch (trigger)
        {
            case TriggerType.OnPressed: return ref _onPressed;
            case TriggerType.OnReleased: return ref _onReleased;
            case TriggerType.OnRepeated: return ref _onRepeated;
            default: return ref _onPressed;
        }
    }



    private bool KeybindIsEmpty() => _onPressed == null && _onReleased == null && _onRepeated == null;



    // disposal



    public override void Dispose()
    {
        _onPressed?.Dispose();
        _onReleased?.Dispose();
        _onRepeated?.Dispose();
    }
}