using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Chatbot.Actions;



public class DeleteCommandAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Delete Command";
    [JsonIgnore] public override string Description => "Deletes a command from the Editable commands (e.g. for !delcom). It has to be a whole command that runs a binding, not a prefix or a row that leads into more blocks. A failure is logged, except when a chat message ran it (then it fails quietly, so nothing after it runs).";



    // private



    private readonly ValueSlot<string> _command;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("command")]
    private ValueSlotData JsonCommand
    {
        get => _command.GetData();
        set => _command.LoadData(value);
    }



    // constructor



    public DeleteCommandAction()
    {
        _command = new("Command");
        _slots = [_command];

        // the parameter a #delcom command parses (a new block starts with this, a saved one loads its own)
        _command.SetText("{command}");

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var model = GlobalContext.ChatbotComponentModel ?? throw Fail("no chatbot in global context");

        var command = await _command.EvaluateAsync(context);

        // from chat a failure is quiet (chatters get no reply), anywhere else the model logs why
        if (!model.TryDeleteCommand(command, logErrors: !ChatEvents.IsFromChat(context.Parameters)))
            throw new BlockFailedException("the command wasn't deleted");
    }
}
