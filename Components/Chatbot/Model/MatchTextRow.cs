using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Chatbot.Model;



// one row of a Match Text block: a pattern, and what happens when it matches (a binding, or another chat block)
public sealed class MatchTextRow : IDisposable
{
    // private



    private string _pattern;
    private readonly ChatTarget _target;
    private readonly ChatPattern _compiled;



    // public



    public string Pattern => _pattern;
    public ChatTarget Target => _target;
    public ChatPattern Compiled => _compiled;
    public bool IsEmpty => _pattern == "" && _target.IsEmpty;



    // events



    public event Action? PatternChanged;



    // constructor



    public MatchTextRow()
    {
        _pattern = "";
        _target = new();
        _compiled = new();
    }



    // public api



    public void SetPattern(string pattern)
    {
        if (pattern == _pattern) return;

        _pattern = pattern;
        PatternChanged?.Invoke();
    }



    // matching (see ChatText)



    public bool IsRegex => ChatText.TryGetRegex(_pattern, out _);



    // the length matched from state.Position (spaces skipped before a plain row included), and any named groups a
    // /regex/ captured, or false. A row with no text matches nothing (it's the empty row at the end)
    public bool TryMatchAt(ChatMatchState state, string blockName, out int length, out IEnumerable<KeyValuePair<string, string>> groups)
    {
        length = 0;
        groups = [];

        if (ChatText.TryGetRegex(_pattern, out var body))
        {
            var match = _compiled.MatchAt(body, ignoreCase: true, state.Text, state.Position, blockName);
            if (match == null) return false;

            length = match.Length;
            groups = _compiled.NamedGroups(match);
            return true;
        }

        var text = _pattern.Trim();
        if (text == "") return false;

        if (ChatText.SkipGap(state.Text, state.Position, ChatText.IsWordChar(text[0])) is not { } start) return false;

        var end = start + text.Length;
        if (end > state.Text.Length || string.Compare(state.Text, start, text, 0, text.Length, StringComparison.OrdinalIgnoreCase) != 0) return false;
        if (!ChatText.EndsCleanly(state.Text, end, text)) return false;

        length = end - state.Position;
        return true;
    }



    // json



    public MatchTextRowData GetData()
    {
        var target = _target.GetData();
        return new() { Pattern = _pattern, Bindable = target.Bindable, Child = target.Child };
    }



    public void LoadData(MatchTextRowData data)
    {
        _pattern = data.Pattern;
        _target.LoadData(new() { Bindable = data.Bindable, Child = data.Child });
    }



    // disposal



    public void Dispose() => _target.Dispose();
}



// how a row is saved
public class MatchTextRowData
{
    [JsonPropertyName("pattern")]
    public string Pattern { get; set; } = "";

    [JsonPropertyName("bindable")]
    public BindableModel? Bindable { get; set; }

    [JsonPropertyName("child")]
    public PendingBlockData? Child { get; set; }
}
