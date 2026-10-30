using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OBS.Background;
using StreamAssist.Shell;

namespace StreamAssist.Components.OBS.Actions;



public class SetObsTextAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Set OBS Text";
    [JsonIgnore] public override string Description => "Writes into a text source (its \"text\" setting), e.g. \"{user} just raided\". Parameters work in the text.";



    // private



    private readonly ValueSlot<string> _source;
    private readonly ValueSlot<string> _text;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("source")]
    private ValueSlotData JsonSource
    {
        get => _source.GetData();
        set => _source.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("text")]
    private ValueSlotData JsonText
    {
        get => _text.GetData();
        set => _text.LoadData(value);
    }



    // constructor



    public SetObsTextAction()
    {
        _source = new("Source");
        _text = new("Text");
        _slots = [_source, _text];

        TrackSlots();
    }



    // execute



    // OBS requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var obs = GlobalContext.ObsManager ?? throw Fail("obs not set up");
        var source = await _source.EvaluateAsync(context);
        var text = await _text.EvaluateAsync(context);

        if (!(await obs.TrySetSourceSettingAsync(source, "text", text)).Ok) throw new BlockFailedException("text not set");
    }
}
