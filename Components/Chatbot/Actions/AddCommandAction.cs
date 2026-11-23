using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Model;

namespace StreamAssist.Components.Chatbot.Actions;



public class AddCommandAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Add Command";
    [JsonIgnore] public override string Description => "Adds a command that replies with the response (e.g. for !addcom) to the first Editable Commands block it fits (one after a ! row takes !hello). Fails if it already exists anywhere, or no Editable Commands block fits it. If it fails from chat it replies (e.g. 'command not found'), anywhere else it logs why, and either way nothing after it runs.";
    [JsonIgnore] public override string? Subgroup => "Commands";



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
        var command = await _command.EvaluateAsync(context);
        var response = await _response.EvaluateAsync(context);
        await CommandActionHelper.RunAsync(context, twitch => twitch.AddCommand(command, response));
    }
}
