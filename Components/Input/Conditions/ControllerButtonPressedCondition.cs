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
        var controllerOption = await _controller.EvaluateAsync(context);
        var buttonOption = await _button.EvaluateAsync(context);
        if (GlobalContext.DeviceInputManager is not { } input)
        {
            AppLog.WriteError($"{Name}: input not set up");
            return false;
        }

        if (InputDevices.ControllerButtons.FirstOrDefault(b => b.Name == buttonOption) is not { Name: not null } button)
        {
            AppLog.WriteError($"{Name}: no controller button '{buttonOption}'");
            return false;
        }

        if (controllerOption == AnyController) return input.IsKeyDown(button.Code);

        if (DeviceInputManager.Devices.FirstOrDefault(d => d.Kind == DeviceKind.Controller && d.GetDisplayLabel() == controllerOption) is not { } controller)
        {
            AppLog.WriteWarning($"{Name}: controller '{controllerOption}' not connected");
            return false;
        }

        return input.IsKeyDown(controller.DevicePath, button.Code);
    }
}
