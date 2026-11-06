using StreamAssist.Components.Input.Model;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Input.UI;



// one kind of input device in the input device list, dragged into the boxes to add one
public sealed class InputDeviceCardViewModel(string layout) : ViewModelBase, IDraggable
{
    public string Name => layout switch
    {
        InputSlotModel.AllKeysLayout => "Generic",
        InputSlotModel.MouseLayout => "Mouse",
        InputSlotModel.ControllerLayout => "Controller",
        InputSlotModel.MicrophoneLayout => "Microphone",
        _ => "Keyboard",
    };

    public string Description => layout switch
    {
        InputSlotModel.AllKeysLayout => "Every code in a grid: keys (F13-F24, media keys, ...), mouse buttons and wheel, controller buttons; for any device",
        InputSlotModel.MouseLayout => "A mouse picture: its buttons, the wheel's steps and tilt (a bound click still clicks)",
        InputSlotModel.ControllerLayout => "An Xbox-style controller (XInput): buttons, triggers, D-pad and stick directions",
        InputSlotModel.MicrophoneLayout => "A mic or any audio input (e.g. a Voicemeeter output): its Talking button is pressed while you talk",
        _ => "A keyboard picture, for a normal keyboard",
    };

    public object? GetDragPayload() => new NewInputDevicePayload(layout);

    public override void Dispose() { }
}
