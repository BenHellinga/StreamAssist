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
    [JsonIgnore] public override string Description => "Reads the next part of the message into a parameter with this name, then continues into the blocks inside. The pattern decides how much: \\S+ is one word, .* is the rest of the message.";



    // private



    private string _parameterName;
    private string _pattern;
    private readonly ChatPattern _compiled;
    private readonly BlockSlotList _children;
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

    [JsonIgnore] public BlockSlotList Children => _children;
    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("children")]
    private List<PendingBlockData> JsonChildren
    {
        get => _children.GetData();
        set => _children.LoadData(value);
    }



    // constructor



    public ParameterChatBlock()
    {
        _parameterName = "";
        _pattern = @"\S+";
        _compiled = new();
        _children = new("", typeof(ChatBlock));
        _slots = [_children];

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

        return TryMatchList(_children, state.Advance(match.Length, parameters));
    }
}
