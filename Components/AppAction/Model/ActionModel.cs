using System;
using System.Threading.Tasks;

namespace StreamAssist.Components.AppAction.Model;



public interface ActionModel
{
    string Name { get; }
    string Description { get; }
    event Action? ConfigurationChanged;
    Task SafeExecuteAsync();
}
