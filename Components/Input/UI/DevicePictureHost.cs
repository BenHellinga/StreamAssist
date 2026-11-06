using Avalonia;
using Avalonia.Controls;

namespace StreamAssist.Components.Input.UI;



// holds a keyboard picture, which is drawn as wide as this is given (InputSlotView sets its scale from this
// width). It asks for no width itself: otherwise, inside a list that can scroll sideways, the picture would ask for the
// width it was last drawn at and never get narrower again
public class DevicePictureHost : Decorator
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        return new Size(0, size.Height);
    }
}
