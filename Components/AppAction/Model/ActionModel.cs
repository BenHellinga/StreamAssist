using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace StreamAssist.Components.AppAction.Model;



public abstract class ActionModel
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public event Action? ConfigurationChanged;
    public abstract Task SafeExecuteAsync();



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
}