using System.Threading.Tasks;

namespace StreamAssist.Components.AppAction.Model;



public interface IAppAction
{
    string Name { get; }
    string Description { get; }
    Task SafeExecuteAsync();
}
