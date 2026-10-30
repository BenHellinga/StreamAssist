using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Chatbot.Actions;



public class EditCommandAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Edit Command";
    [JsonIgnore] public override string Description => "Changes what a command in the Editable commands replies (e.g. for !editcom). Only works on commands that just send a message. If it fails from chat it replies (e.g. 'command not found'), anywhere else it logs why, and either way nothing after it runs.";



    // private



    private readonly ValueSlot<string> _command;
    private readonly ValueSlot<string> _response;
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

    [JsonInclude] [JsonPropertyName("response")]
    private ValueSlotData JsonResponse
    {
        get => _response.GetData();
        set => _response.LoadData(value);
    }



    // constructor



    public EditCommandAction()
    {
        _command = new("Command");
        _response = new("Response");
        _slots = [_command, _response];

        // the parameters a #editcom command parses (a new block starts with these, a saved one loads its own)
        _command.SetText("{command}");
        _response.SetText("{content}");

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var model = GlobalContext.ChatbotComponentModel ?? throw Fail("chatbot not set up");

        var command = await _command.EvaluateAsync(context);
        var response = await _response.EvaluateAsync(context);

        // from chat the chatter is told what went wrong, anywhere else the model logs it to the console
        if (!model.TryEditCommand(command, response, out var failure, logErrors: !ChatEvents.IsFromChat(context.Parameters)))
            throw await CommandActionHelper.FailAsync(context, failure, "the command wasn't edited");
    }
}
