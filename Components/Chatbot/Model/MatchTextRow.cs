using System;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Chatbot.Model;



// one row of a Match Text block: a pattern, and the chat block that continues after it matches
public sealed class MatchTextRow : IDisposable
{
    // private



    private string _pattern;
    private readonly BlockSlot _child;
    private readonly ChatPattern _compiled;



    // public



    public string Pattern => _pattern;
    public BlockSlot Child => _child;
    public ChatPattern Compiled => _compiled;
    public bool IsEmpty => _pattern == "" && _child.Block == null;



    // events



    public event Action? PatternChanged;



    // constructor



    public MatchTextRow()
    {
        _pattern = "";
        _child = new("", typeof(ChatBlock));
        _compiled = new();
    }



    // public api



    public void SetPattern(string pattern)
    {
        if (pattern == _pattern) return;

        _pattern = pattern;
        PatternChanged?.Invoke();
    }



    // json



    public MatchTextRowData GetData() => new() { Pattern = _pattern, Child = _child.GetData() };



    public void LoadData(MatchTextRowData data)
    {
        _pattern = data.Pattern;
        _child.LoadData(data.Child);
    }



    // disposal



    public void Dispose() => _child.Dispose();
}



// how a row is saved
public class MatchTextRowData
{
    [JsonPropertyName("pattern")]
    public string Pattern { get; set; } = "";

    [JsonPropertyName("child")]
    public PendingBlockData? Child { get; set; }
}
