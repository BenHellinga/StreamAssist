using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Chatbot.Actions;



public class AddCommandAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Add Command";
    [JsonIgnore] public override string Description => "Adds a command to the Editable commands that replies with the response (e.g. for !addcom). Fails if it already exists. A failure is logged, except when a chat message ran it (then it fails quietly, so nothing after it runs).";



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



    public AddCommandAction()
    {
        _command = new("Command");
        _response = new("Response");
        _slots = [_command, _response];

        // the parameters a #addcom command parses (a new block starts with these, a saved one loads its own)
        _command.SetText("{command}");
        _response.SetText("{content}");

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var model = GlobalContext.ChatbotComponentModel ?? throw Fail("no chatbot in global context");

        var command = await _command.EvaluateAsync(context);
        var response = await _response.EvaluateAsync(context);

        // from chat a failure is quiet (chatters get no reply), anywhere else the model logs why
        if (!model.TryAddCommand(command, response, logErrors: !ChatEvents.IsFromChat(context.Parameters)))
            throw new BlockFailedException("the command wasn't added");
    }
}
