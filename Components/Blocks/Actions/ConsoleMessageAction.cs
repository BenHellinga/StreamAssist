using System.Collections.Generic;
using System.Threading.Tasks;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Components.Blocks.Model;
using System.Text.Json.Serialization;

namespace StreamAssist.Components.Blocks.Actions;



public class ConsoleMessageAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Console Message";
    [JsonIgnore] public override string Description => "Prints the message to the console (type it, {name} is replaced with a parameter, e.g. {user}, or place a text block).";



    // private



    private readonly ValueSlot<string> _message;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("message")]
    private ValueSlotData JsonMessage
    {
        get => _message.GetData();
        set => _message.LoadData(value);
    }



    // constructor



    public ConsoleMessageAction()
    {
        _message = new("Message");
        _slots = [_message];

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        AppLog.WriteLine(await _message.EvaluateAsync(context));
    }
}
