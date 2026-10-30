using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OBS.Background;
using StreamAssist.Shell;

namespace StreamAssist.Components.OBS.Actions;



public class SetObsSourceSettingAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Set OBS Source Setting";
    [JsonIgnore] public override string Description => "Sets any setting of a source by its name, like its Properties window (e.g. \"text\" for text sources, \"url\" for browser sources, \"file\" for images). The value keeps the kind the setting already is.";



    // private



    private readonly ValueSlot<string> _source;
    private readonly ValueSlot<string> _setting;
    private readonly AnyValueSlot _value;
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

    [JsonInclude] [JsonPropertyName("setting")]
    private ValueSlotData JsonSetting
    {
        get => _setting.GetData();
        set => _setting.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("value")]
    private ValueSlotData JsonValue
    {
        get => _value.GetData();
        set => _value.LoadData(value);
    }



    // constructor



    public SetObsSourceSettingAction()
    {
        _source = new("Source");
        _setting = new("Setting");
        _value = new("Value");
        _slots = [_source, _setting, _value];

        TrackSlots();
    }



    // execute



    // OBS requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var obs = GlobalContext.ObsManager ?? throw Fail("obs not set up");
        var source = await _source.EvaluateAsync(context);
        var setting = await _setting.EvaluateAsync(context);
        if (setting.Trim() == "") throw Fail("needs a setting name");

        // typed text is passed as text, a block's value as its kind, the manager turns it into the setting's kind
        var (_, value) = await _value.EvaluateRawAsync(context);

        if (!(await obs.TrySetSourceSettingAsync(source, setting.Trim(), value)).Ok) throw new BlockFailedException("setting not set");
    }
}
