using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.AppAction.Model;



public abstract class ActionModel : IDisposable
{
    // private



    private bool _isDisabled;



    // public



    [JsonIgnore] public abstract string Name { get; }
    [JsonIgnore] public abstract string Description { get; }
    [JsonIgnore] public bool IsDisabled => _isDisabled;



    // json



    [JsonInclude] [JsonPropertyName("isDisabled")]
    private bool JsonIsDisabled
    {
        get => _isDisabled;
        set => _isDisabled = value;
    }



    // events



    public event Action? ConfigurationChanged;
    public event Action<bool>? IsDisabledChanged;
    public event Action? Triggered;



    // constructor



    protected ActionModel()
    {
        _isDisabled = false;
    }



    // public api



    public async Task SafeExecuteAsync()
    {
        if (_isDisabled) return;

        try
        {
            Execute();
        }
        catch (Exception ex)
        {
            AppLog.WriteError($"Action '{Name}' thew an exception: {ex.Message}.");
        }

        Triggered?.Invoke();
    }



    public void SetDisabled(bool isDisabled)
    {
        if (isDisabled == _isDisabled) return;

        _isDisabled = isDisabled;
        IsDisabledChanged?.Invoke(_isDisabled);
    }



    protected void SetParameter<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        ConfigurationChanged?.Invoke();
    }



    public static ActionModel DeepCopy(ActionModel action)
    {
        var data = ActionResolver.GetKey(action);
        return ActionResolver.Resolve(data)!;
    }



    // private



    protected abstract void Execute();



    // disposal



    public virtual void Dispose() {}
}