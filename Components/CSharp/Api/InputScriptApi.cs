using System.Collections.Generic;
using System.Linq;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Input.Background;
using StreamAssist.Components.Input.Model;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Scripting;
using StreamAssist.Shell;
using StreamAssist.Core.Utilities;

namespace StreamAssist.Components.CSharp.Api;



// IInput: what the device input manager says is held, and the Microphone boxes' talking
public sealed class InputScriptApi(TriggerContext context) : ScriptApiPart(context), IInput
{
    private static DeviceInputManager Devices => Need(GlobalContext.DeviceInputManager, "input");



    public bool IsKeyDown(string key, string? keyboard = null) => UiThread.Run<bool>(() =>
    {
        var input = Devices;
        if (!KeyNames.TryParse(key, out var vk)) throw Error($"'{key}' isn't a key");
        return IsDown(input, vk, DeviceKind.Keyboard, "keyboard", keyboard);
    });



    public bool IsMouseButtonDown(string button, string? mouse = null) => UiThread.Run<bool>(() =>
    {
        var input = Devices;
        var found = InputDevices.MouseButtons.FirstOrDefault(b => b.Name == button);
        if (found.Name == null) throw Error($"no mouse button '{button}'");
        return IsDown(input, found.Code, DeviceKind.Mouse, "mouse", mouse);
    });



    public bool IsControllerButtonDown(string button, string? controller = null) => UiThread.Run<bool>(() =>
    {
        var input = Devices;
        var found = InputDevices.ControllerButtons.FirstOrDefault(b => b.Name == button);
        if (found.Name == null) throw Error($"no controller button '{button}'");
        return IsDown(input, found.Code, DeviceKind.Controller, "controller", controller);
    });



    public bool IsTalking(string? microphone = null) => UiThread.Run<bool>(() =>
    {
        var input = Need(GlobalContext.InputComponentModel, "input");
        var boxes = input.ListModel.Slots.Where(s => s.Layout == InputSlotModel.MicrophoneLayout && s.SelectedDevice != null).ToList();
        if (microphone == null) return boxes.Any(s => s.IsTalking);

        boxes = boxes.Where(s => s.SelectedDevice!.GetDisplayLabel() == microphone).ToList();
        if (boxes.Count == 0)
        {
            AppLog.WriteWarning($"no Microphone box listens to '{microphone}'");
            return false;
        }
        return boxes.Any(s => s.IsTalking);
    });



    public IReadOnlyList<string> Keyboards => UiThread.Run<IReadOnlyList<string>>(() => Names(DeviceKind.Keyboard));
    public IReadOnlyList<string> Mice => UiThread.Run<IReadOnlyList<string>>(() => Names(DeviceKind.Mouse));
    public IReadOnlyList<string> Controllers => UiThread.Run<IReadOnlyList<string>>(() => Names(DeviceKind.Controller));
    public IReadOnlyList<string> Microphones => UiThread.Run<IReadOnlyList<string>>(() => Names(DeviceKind.Microphone));
    public IReadOnlyList<string> MouseButtons => UiThread.Run<IReadOnlyList<string>>(() => InputDevices.MouseButtons.Select(b => b.Name).ToList());
    public IReadOnlyList<string> ControllerButtons => UiThread.Run<IReadOnlyList<string>>(() => InputDevices.ControllerButtons.Select(b => b.Name).ToList());



    // helpers



    // on any device (null) or the one named; a device that isn't connected is a warning and counts as not held
    private static bool IsDown(DeviceInputManager input, ushort code, DeviceKind kind, string what, string? device)
    {
        if (device == null) return input.IsKeyDown(code);

        var found = DeviceInputManager.Devices.FirstOrDefault(d => d.Kind == kind && d.GetDisplayLabel() == device);
        if (found == null)
        {
            AppLog.WriteWarning($"{what} '{device}' not connected");
            return false;
        }
        return input.IsKeyDown(found.DevicePath, code);
    }



    private static List<string> Names(DeviceKind kind) =>
        DeviceInputManager.Devices.Where(d => d.Kind == kind).Select(d => d.GetDisplayLabel()).ToList();
}
