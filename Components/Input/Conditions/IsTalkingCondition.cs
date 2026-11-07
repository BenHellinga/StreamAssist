using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Input.Background;
using StreamAssist.Components.Input.Model;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Input.Conditions;



// true while you're talking into the chosen microphone (or any): a Microphone box on that input says so (by its own
// threshold and hold), so it needs one
public class IsTalkingCondition : ConditionBlock
{
    // info



    [JsonIgnore] public override string Name => "Is Talking";
    [JsonIgnore] public override string Description => "True while you're talking into the chosen microphone (or any), as its Microphone box on the Input tab decides (its threshold and hold).";



    // constants



    private const string AnyMicrophone = "Any microphone";



    // private



    private readonly MenuSlot _microphone;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("microphone")]
    private ValueSlotData JsonMicrophone
    {
        get => _microphone.GetData();
        set => _microphone.LoadData(value);
    }



    // constructor



    public IsTalkingCondition()
    {
        _microphone = new("Microphone", GetMicrophoneMenuItems, AnyMicrophone, isFixed: false);
        _slots = [_microphone];

        TrackSlots();
    }



    // menus (microphones by their Windows name, as a Microphone box's dropdown shows them)



    public static List<string> GetMicrophoneMenuItems() =>
        [AnyMicrophone, .. DeviceInputManager.Devices.Where(d => d.Kind == DeviceKind.Microphone).Select(d => d.GetDisplayLabel())];



    // check



    protected override async Task<bool> CheckAsync(TriggerContext context)
    {
        var microphone = await _microphone.EvaluateAsync(context);
        if (GlobalContext.InputComponentModel is not { } input)
        {
            AppLog.WriteError($"{Name}: input not set up");
            return false;
        }

        var boxes = input.ListModel.Slots.Where(s => s.Layout == InputSlotModel.MicrophoneLayout && s.SelectedDevice != null).ToList();
        if (microphone != AnyMicrophone)
        {
            boxes = boxes.Where(s => s.SelectedDevice!.GetDisplayLabel() == microphone).ToList();
            if (boxes.Count == 0)
            {
                AppLog.WriteWarning($"{Name}: no Microphone box listens to '{microphone}'");
                return false;
            }
        }

        return boxes.Any(s => s.IsTalking);
    }
}
