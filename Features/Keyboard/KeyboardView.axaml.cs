using Avalonia.Controls;

namespace StreamAssist.Features.Keyboard;



public partial class KeyboardView : UserControl
{
    public KeyboardView()
    {
        Resources["UnitToPixelConverter"] = UnitToPixelConverter.Instance;
        InitializeComponent();
    }
}
