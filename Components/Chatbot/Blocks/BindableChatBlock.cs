using System.Collections.Generic;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Model;

namespace StreamAssist.Components.Chatbot.Blocks;



// a binding slot inside the chat tree: runs its bindable when a message matches everything up to here
// clicking it in the chatbot tab opens its bindable in the bottom panel editor
public class BindableChatBlock : ChatBlock
{
    // info



    [JsonIgnore] public override string Name => "Bindable";
    [JsonIgnore] public override string Description => "Runs its blocks when a message matches up to here. Click it to edit them below.";



    // private



    private BindableModel _bindable;



    // public



    [JsonIgnore] public BindableModel Bindable => _bindable;



    // json



    [JsonInclude] [JsonPropertyName("bindable")]
    private BindableModel JsonBindable
    {
        get => _bindable;
        set => SetBindable(value);
    }



    // constructor



    public BindableChatBlock()
    {
        _bindable = new();
        _bindable.Changed += OnBindableChanged;
    }



    // matching



    // reaching this block means the message matched, whatever text is left over is passed along as {rest}
    public override bool TryMatch(ChatMatchState state)
    {
        var parameters = new Dictionary<string, string>(state.Parameters)
        {
            ["rest"] = state.Text[state.Position..],
        };

        RaiseTriggered();
        _bindable.TriggerOnce(parameters);
        return true;
    }



    // helpers



    private void SetBindable(BindableModel bindable)
    {
        _bindable.Changed -= OnBindableChanged;
        _bindable.Dispose();

        _bindable = bindable;
        _bindable.Changed += OnBindableChanged;
    }



    // changes inside the bindable count as changes to this block (so they get saved)
    private void OnBindableChanged() => RaiseChanged();



    // disposal



    public override void Dispose()
    {
        _bindable.Changed -= OnBindableChanged;
        _bindable.Dispose();
        base.Dispose();
    }
}
