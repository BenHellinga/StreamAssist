using Avalonia.Controls;

namespace StreamAssist.Features.Keyboard;



public partial class KeyboardDisplay : UserControl
{
    public KeyboardDisplay()
    {
        Resources["UnitToPixelConverter"] = UnitToPixelConverter.Instance;
        InitializeComponent();
    }
}
