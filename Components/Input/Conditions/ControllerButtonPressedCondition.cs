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



    private string _controller;
    private string _button;



    // public



    [ConfigurableParameter("Controller", ConfiguratorType.Menu)] [JsonPropertyName("controller")]
    public string Controller
    {
        get => _controller;
        set => SetParameter(ref _controller, value);
    }



    [ConfigurableParameter("Button", ConfiguratorType.Menu)] [JsonPropertyName("button")]
    public string Button
    {
        get => _button;
        set => SetParameter(ref _button, value);
    }



    // constructor



    public ControllerButtonPressedCondition()
    {
        _controller = AnyController;
        _button = InputDevices.ControllerButtons[0].Name;
    }



    // menus (a controller is its XInput slot, "Controller 1" to "Controller 4")



    public static List<string> GetControllerMenuItems() =>
        [AnyController, .. DeviceInputManager.Devices.Where(d => d.Kind == DeviceKind.Controller).Select(d => d.GetDisplayLabel())];

    public static List<string> GetButtonMenuItems() => InputDevices.ControllerButtons.Select(b => b.Name).ToList();



    // check



    protected override Task<bool> CheckAsync(TriggerContext context)
    {
        if (GlobalContext.DeviceInputManager is not { } input)
        {
            AppLog.WriteError($"{Name}: input not set up");
            return Task.FromResult(false);
        }

        if (InputDevices.ControllerButtons.FirstOrDefault(b => b.Name == _button) is not { Name: not null } button)
        {
            AppLog.WriteError($"{Name}: no controller button '{_button}'");
            return Task.FromResult(false);
        }

        if (_controller == AnyController) return Task.FromResult(input.IsKeyDown(button.Code));

        if (DeviceInputManager.Devices.FirstOrDefault(d => d.Kind == DeviceKind.Controller && d.GetDisplayLabel() == _controller) is not { } controller)
        {
            AppLog.WriteWarning($"{Name}: controller '{_controller}' not connected");
            return Task.FromResult(false);
        }

        return Task.FromResult(input.IsKeyDown(controller.DevicePath, button.Code));
    }
}
