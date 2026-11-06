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



    private string _mouse;
    private string _button;



    // public



    [ConfigurableParameter("Mouse", ConfiguratorType.Menu)] [JsonPropertyName("mouse")]
    public string Mouse
    {
        get => _mouse;
        set => SetParameter(ref _mouse, value);
    }



    [ConfigurableParameter("Button", ConfiguratorType.Menu)] [JsonPropertyName("button")]
    public string Button
    {
        get => _button;
        set => SetParameter(ref _button, value);
    }



    // constructor



    public MouseButtonPressedCondition()
    {
        _mouse = AnyMouse;
        _button = InputDevices.MouseButtons[0].Name;
    }



    // menus (mice are saved by their display label, the text a Mouse box's dropdown shows)



    public static List<string> GetMouseMenuItems() =>
        [AnyMouse, .. DeviceInputManager.Devices.Where(d => d.Kind == DeviceKind.Mouse).Select(d => d.GetDisplayLabel())];

    public static List<string> GetButtonMenuItems() => InputDevices.MouseButtons.Select(b => b.Name).ToList();



    // check



    protected override Task<bool> CheckAsync(TriggerContext context)
    {
        if (GlobalContext.DeviceInputManager is not { } input)
        {
            AppLog.WriteError($"{Name}: input not set up");
            return Task.FromResult(false);
        }

        if (InputDevices.MouseButtons.FirstOrDefault(b => b.Name == _button) is not { Name: not null } button)
        {
            AppLog.WriteError($"{Name}: no mouse button '{_button}'");
            return Task.FromResult(false);
        }

        if (_mouse == AnyMouse) return Task.FromResult(input.IsKeyDown(button.Code));

        if (DeviceInputManager.Devices.FirstOrDefault(d => d.Kind == DeviceKind.Mouse && d.GetDisplayLabel() == _mouse) is not { } mouse)
        {
            AppLog.WriteWarning($"{Name}: mouse '{_mouse}' not connected");
            return Task.FromResult(false);
        }

        return Task.FromResult(input.IsKeyDown(mouse.DevicePath, button.Code));
    }
}
