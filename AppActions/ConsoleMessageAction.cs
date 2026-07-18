using System.Threading.Tasks;
using StreamAssist.Components.Main.Model;
using StreamAssist.Components.AppAction.Model;
using System.Text.Json.Serialization;
using StreamAssist.Components.Core.Configurator;

namespace StreamAssist.AppActions;



public class ConsoleMessageAction : ActionModel
{
    // info



    [JsonIgnore] public override string Name => "Console Message";
    [JsonIgnore] public override string Description => "Prints the configured message to the console.";



    // private



    private string _message;



    // public



    [ConfigurableParameter("Message", ConfiguratorType.Text)] [JsonPropertyName("message")]
    public string Message
    {
        get => _message;
        set => SetParameter(ref _message, value);
    }



    // constructor



    public ConsoleMessageAction()
    {
        _message = "";
    }



    // execute



    public override Task SafeExecuteAsync()
    {
        AppLog.WriteLine(_message);
        return Task.CompletedTask;
    }
}