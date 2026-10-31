using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Conditions;



public class IsEmptyCondition : ConditionBlock
{
    // info



    [JsonIgnore] public override string Name => "Is Empty";
    [JsonIgnore] public override string Description => "True if the text is empty or only spaces, e.g. {quote} when !addquote was typed with nothing after it.";
    [JsonIgnore] public override BlockGroup Group => BlockGroup.Text;



    // private



    private readonly ValueSlot<string> _text;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("text")]
    private ValueSlotData JsonText
    {
        get => _text.GetData();
        set => _text.LoadData(value);
    }



    // constructor



    public IsEmptyCondition()
    {
        _text = new("Text");
        _slots = [_text];

        TrackSlots();
    }



    // check



    protected override async Task<bool> CheckAsync(TriggerContext context) => (await _text.EvaluateAsync(context)).Trim() == "";
}
