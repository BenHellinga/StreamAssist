using StreamAssist.Components.Keyboard.Model;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Keyboard.UI;



// one kind of input device in the input device list, dragged into the keyboards to add one
public sealed class InputDeviceCardViewModel(string layout) : ViewModelBase, IDraggable
{
    public string Name => layout == KeyboardSlotModel.AllKeysLayout ? "Generic" : "Keyboard";

    public string Description => layout == KeyboardSlotModel.AllKeysLayout
        ? "Every key code Windows has in a grid (F13-F24, media keys, ...), for macro pads and other devices"
        : "A keyboard picture, for a normal keyboard";



    public object? GetDragPayload() => new NewInputDevicePayload(layout);



    public override void Dispose() { }
}
