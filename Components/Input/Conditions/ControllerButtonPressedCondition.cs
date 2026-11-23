using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Input.Background;
using StreamAssist.Components.Input.Model;

namespace StreamAssist.Components.Input.Conditions;



// true while a controller button is held (a trigger pulled or a stick pushed past halfway counts too), on any
// controller or the chosen one
public class ControllerButtonPressedCondition : ConditionBlock
{
    // info



    [JsonIgnore] public override string Name => "Controller Button Pressed";
    [JsonIgnore] public override string Description => "True while the button is held on the chosen controller (or any). Triggers and stick directions count once pushed past halfway.";



    // constants



    private const string AnyController = "Any controller";



    // private



    private readonly MenuSlot _controller;
    private readonly MenuSlot _button;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("controller")]
    private ValueSlotData JsonController
    {
        get => _controller.GetData();
        set => _controller.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("button")]
    private ValueSlotData JsonButton
    {
        get => _button.GetData();
        set => _button.LoadData(value);
    }



    // constructor



    public ControllerButtonPressedCondition()
    {
        _controller = new("Controller", GetControllerMenuItems, AnyController, isFixed: false);
        _button = new("Button", GetButtonMenuItems, InputDevices.ControllerButtons[0].Name);
        _slots = [_controller, _button];

        TrackSlots();
    }



    // menus (a controller is its XInput slot, "Controller 1" to "Controller 4")



    public static List<string> GetControllerMenuItems() =>
        [AnyController, .. DeviceInputManager.Devices.Where(d => d.Kind == DeviceKind.Controller).Select(d => d.GetDisplayLabel())];

    public static List<string> GetButtonMenuItems() => InputDevices.ControllerButtons.Select(b => b.Name).ToList();



    // check



    protected override async Task<bool> CheckAsync(TriggerContext context)
    {
        var controller = await _controller.EvaluateAsync(context);
        var button = await _button.EvaluateAsync(context);
        return Quietly(() => Api(context).Input.IsControllerButtonDown(button, controller == AnyController ? null : controller));
    }
}
