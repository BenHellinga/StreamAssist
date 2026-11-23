using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Input.Background;
using StreamAssist.Components.Input.Model;

namespace StreamAssist.Components.Input.Conditions;



// true while a mouse button is held, on any mouse or the chosen one (the wheel only steps, so it isn't here: bind its
// steps on a Mouse box instead)
public class MouseButtonPressedCondition : ConditionBlock
{
    // info



    [JsonIgnore] public override string Name => "Mouse Button Pressed";
    [JsonIgnore] public override string Description => "True while the button is held down on the chosen mouse (or any mouse).";



    // constants



    private const string AnyMouse = "Any mouse";



    // private



    private readonly MenuSlot _mouse;
    private readonly MenuSlot _button;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("mouse")]
    private ValueSlotData JsonMouse
    {
        get => _mouse.GetData();
        set => _mouse.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("button")]
    private ValueSlotData JsonButton
    {
        get => _button.GetData();
        set => _button.LoadData(value);
    }



    // constructor



    public MouseButtonPressedCondition()
    {
        _mouse = new("Mouse", GetMouseMenuItems, AnyMouse, isFixed: false);
        _button = new("Button", GetButtonMenuItems, InputDevices.MouseButtons[0].Name);
        _slots = [_mouse, _button];

        TrackSlots();
    }



    // menus (mice are saved by their display label, the text a Mouse box's dropdown shows)



    public static List<string> GetMouseMenuItems() =>
        [AnyMouse, .. DeviceInputManager.Devices.Where(d => d.Kind == DeviceKind.Mouse).Select(d => d.GetDisplayLabel())];

    public static List<string> GetButtonMenuItems() => InputDevices.MouseButtons.Select(b => b.Name).ToList();



    // check



    protected override async Task<bool> CheckAsync(TriggerContext context)
    {
        var mouse = await _mouse.EvaluateAsync(context);
        var button = await _button.EvaluateAsync(context);
        return Quietly(() => Api(context).Input.IsMouseButtonDown(button, mouse == AnyMouse ? null : mouse));
    }
}
