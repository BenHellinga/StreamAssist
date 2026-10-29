using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Model;

namespace StreamAssist.Components.Chatbot.Blocks;



public class ParameterChatBlock : ChatBlock
{
    // info



    [JsonIgnore] public override string Name => "Parameter";
    [JsonIgnore] public override string Description => "Reads the next part of the message into a parameter with this name, then runs its binding or continues into the block next to it. The pattern decides how much: \\S+ is one word, .* is the rest of the message.";



    // private



    private string _parameterName;
    private string _pattern;
    private readonly ChatPattern _compiled;
    private readonly ChatTarget _target;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Name", ConfiguratorType.Text)] [JsonPropertyName("parameterName")]
    public string ParameterName
    {
        get => _parameterName;
        set => SetParameter(ref _parameterName, value);
    }

    [ConfigurableParameter("Pattern", ConfiguratorType.Text)] [JsonPropertyName("pattern")]
    public string Pattern
    {
        get => _pattern;
        set => SetParameter(ref _pattern, value);
    }

    [JsonIgnore] public ChatTarget Target => _target;
    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("target")]
    private ChatTargetData JsonTarget
    {
        get => _target.GetData();
        set => _target.LoadData(value);
    }



    // constructor



    public ParameterChatBlock()
    {
        _parameterName = "";
        _pattern = @"\S+";
        _compiled = new();
        _target = new();
        _slots = [_target];

        TrackSlots();
    }



    // matching



    public override bool TryMatch(ChatMatchState state)
    {
        var match = _compiled.MatchAt(_pattern, ignoreCase: false, state.Text, state.Position, Name);
        if (match == null) return false;

        RaiseTriggered(); // its pattern matched, flashes like a pressed key (even if nothing after it does)

        var parameters = _compiled.NamedGroups(match).ToList();
        if (!string.IsNullOrWhiteSpace(_parameterName))
            parameters.Add(new(_parameterName.Trim(), match.Value));

        return _target.TryMatch(state.Advance(match.Length, parameters));
    }
}
