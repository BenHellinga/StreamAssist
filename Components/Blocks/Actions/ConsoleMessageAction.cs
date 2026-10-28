using System.Threading.Tasks;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Components.Blocks.Model;
using System.Text.Json.Serialization;

namespace StreamAssist.Components.Blocks.Actions;



public class ConsoleMessageAction : ActionBlock
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



    protected override Task ExecuteAsync(TriggerContext context)
    {
        AppLog.WriteLine(_message);
        return Task.CompletedTask;
    }
}