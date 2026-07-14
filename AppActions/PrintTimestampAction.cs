using System.Threading.Tasks;
using StreamAssist.Components.Main.Model;
using StreamAssist.Components.AppAction.Model;
using System.Text.Json.Serialization;

namespace StreamAssist.AppActions;



public class PrintHelloAction : AppActionBase
{
    [JsonIgnore] public override string Name => "Print Hello";
    [JsonIgnore] public override string Description => "Prints 'Hello, StreamAssist!' to the console.";



    public override Task SafeExecuteAsync()
    {
        AppLog.WriteLine("Hello, StreamAssist!");
        return Task.CompletedTask;
    }
}