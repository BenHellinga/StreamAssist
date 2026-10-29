using System;
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
