using System.Threading.Tasks;
using StreamAssist.Components.Main;
using StreamAssist.Components.AppAction.Model;
using System.Text.Json.Serialization;

namespace StreamAssist.Actions;



public class PrintHelloAction : IAppAction
{
    [JsonIgnore] public string Name => "Print Hello";
    [JsonIgnore] public string Description => "Prints 'Hello, StreamAssist!' to the console.";



    public Task SafeExecuteAsync()
    {
        AppLog.WriteLine("Hello, StreamAssist!");
        return Task.CompletedTask;
    }
}