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
    [JsonIgnore] public override string Description => "Deletes a command from the Custom commands (e.g. for !delcom). It has to be a whole command that runs a binding, not a prefix or a row that leads into more blocks. If it fails from chat it replies (e.g. 'command not found'), anywhere else it logs why, and either way nothing after it runs.";



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
        var model = GlobalContext.ChatbotComponentModel ?? throw Fail("chatbot not set up");

        var command = await _command.EvaluateAsync(context);

        // from chat the chatter is told what went wrong, anywhere else the model logs it to the console
        if (!model.TryDeleteCommand(command, out var failure, logErrors: !ChatEvents.IsFromChat(context.Parameters)))
            throw await CommandActionHelper.FailAsync(context, failure, "the command wasn't deleted");
    }
}
