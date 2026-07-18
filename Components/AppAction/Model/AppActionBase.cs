using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace StreamAssist.Components.AppAction.Model;



public abstract class AppActionBase : ActionModel
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public event Action? ConfigurationChanged;
    public abstract Task SafeExecuteAsync();



    protected void SetParameter<T>(ref T field, T value, [CallerMemberName] string? _ = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        ConfigurationChanged?.Invoke();
    }
}