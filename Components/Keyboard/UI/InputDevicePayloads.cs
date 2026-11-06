using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Components.Keyboard.UI;



// a card from the input device list being dragged into the keyboards (adds a slot of that layout where it's dropped)
public sealed class NewInputDevicePayload(string layout)
{
    public string Layout => layout;
}



// an input device box being dragged by its grip to another place in the list (moves it)
public sealed class KeyboardSlotDragPayload(KeyboardSlotModel slot)
{
    public KeyboardSlotModel Slot => slot;
}
