using System.Collections.Generic;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Model;

namespace StreamAssist.Components.Chatbot.Blocks;



public class ParameterChatBlock : ChatBlock
{
    // info



    [JsonIgnore] public override string Name => "Parse Parameter"; // the class keeps its old name, it's the save file's key
    [JsonIgnore] public override string Description => "Reads the next word, or the rest of the message, into a parameter with this name, then runs its binding or continues into the block next to it (after the rest of the message there's nothing left, so only a binding). Spaces after a word are skipped (and it won't read the end of a word that's already partly matched), after a symbol it reads right away (! then a parameter reads !lurk as lurk).";



    // constants



    public const string NextWord = "Next word";
    public const string RestOfMessage = "Rest of message";



    // private



    private string _parameterName;
    private string _reads;
    private readonly ChatTarget _target;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Name", ConfiguratorType.Text)] [JsonPropertyName("parameterName")]
    public string ParameterName
    {
        get => _parameterName;
        set => SetParameter(ref _parameterName, value);
    }

    [ConfigurableParameter("Reads", ConfiguratorType.Menu)] [JsonPropertyName("reads")]
    public string Reads
    {
        get => _reads;
        set
        {
            SetParameter(ref _reads, value);
            _target.SetEndsHere(_reads == RestOfMessage); // nothing is left to match, so it ends in a binding
        }
    }

    [JsonIgnore] public ChatTarget Target => _target;
    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;
    [JsonIgnore] public override bool SlotsBesideSettings => true; // name: [name] [block slot] [bind box], reads below



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
        _reads = NextWord;
        _target = new();
        _slots = [_target];

        TrackSlots();
    }



    // public



    public static List<string> GetReadsMenuItems() => [NextWord, RestOfMessage];



    // matching



    public override bool TryMatch(ChatMatchState state)
    {
        var text = state.Text;
        var position = state.Position;

        // after a word, skip the spaces in between, but never start in the middle of a word (e.g. "!sofoo" after a
        // "!so" row); after a symbol, read right away (see ChatText)
        if (ChatText.SkipGap(text, position, nextStartsWithWord: true) is not { } start) return false;

        int end;
        string value;
        if (_reads == RestOfMessage)
        {
            end = text.Length;
            value = text[start..].Trim(); // can be empty
        }
        else
        {
            end = start;
            while (end < text.Length && !char.IsWhiteSpace(text[end])) end++;
            if (end == start) return false; // no word left
            value = text[start..end];
        }

        RaiseTriggered(); // it read something, flashes like a pressed key (even if nothing after it matches)

        var parameters = new List<KeyValuePair<string, string>>();
        if (!string.IsNullOrWhiteSpace(_parameterName))
            parameters.Add(new(_parameterName.Trim(), value));

        return _target.TryMatch(state.Advance(end - position, parameters));
    }
}
