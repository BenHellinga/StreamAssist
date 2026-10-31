using StreamAssist.Components.OutputConsole.Actions;
using StreamAssist.Components.Keyboard.Conditions;
using StreamAssist.Components.Audio.Actions;
using StreamAssist.Components.Chatbot.Actions;
using StreamAssist.Components.Blocks.Values;
using StreamAssist.Components.Blocks.Actions;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Components.Chatbot.Background;
using StreamAssist.Components.Chatbot.ChatBlocks;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Components.Blocks.Conditions;
using StreamAssist.Components.Keyboard.Background;
using StreamAssist.Components.Keyboard.Model;
using System.Text.Json;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Blocks.Containers;

namespace StreamAssist.Tests;



// small blocks the checks use to see what ran

public class RecordBlock : ActionBlock
{
    public static List<string> Log = [];
    [JsonIgnore] public override string Name => "Record";
    [JsonIgnore] public override string Description => "test";
    private string _tag2 = "";
    [JsonPropertyName("tag")] public string Tag { get; set; } = "";
    [JsonPropertyName("tag2")] public string Tag2 { get => _tag2; set => SetParameter(ref _tag2, value); }
    [JsonPropertyName("throw")] public bool Throw { get; set; }
    [JsonPropertyName("delay")] public int DelayMs { get; set; }
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        Log.Add(Tag);
        if (DelayMs > 0) { await Task.Delay(DelayMs, context.CancellationToken); Log.Add("late"); }
        if (Throw) throw new InvalidOperationException("boom");
    }
}


public class FixedCondition : ConditionBlock
{
    private bool _value;
    [JsonIgnore] public override string Name => "Fixed";
    [JsonIgnore] public override string Description => "test";
    [JsonPropertyName("value")] public bool Value { get => _value; set => SetParameter(ref _value, value); }
    protected override Task<bool> CheckAsync(TriggerContext context) => Task.FromResult(_value);
}


// records its text with {parameters} filled in, e.g. "{i}" inside a Repeat
public class FormatRecordBlock : ActionBlock
{
    public static List<string> Log = [];
    [JsonIgnore] public override string Name => "FormatRecord";
    [JsonIgnore] public override string Description => "test";
    [JsonPropertyName("template")] public string Template { get; set; } = "";
    protected override Task ExecuteAsync(TriggerContext context)
    {
        lock (Log) Log.Add(context.Format(Template));
        return Task.CompletedTask;
    }
}


// true the first Left times it's checked, then false (to end a While)
public class CountdownCondition : ConditionBlock
{
    [JsonIgnore] public override string Name => "Countdown";
    [JsonIgnore] public override string Description => "test";
    [JsonPropertyName("left")] public int Left { get; set; }
    protected override Task<bool> CheckAsync(TriggerContext context) => Task.FromResult(Left-- > 0);
}
