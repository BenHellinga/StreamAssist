using System.Threading.Tasks;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Components.AppAction.Model;
using System.Text.Json.Serialization;

namespace StreamAssist.Components.AppAction.Actions;



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



    protected override void Execute()
    {
        AppLog.WriteLine(_message);
    }
}