using StreamAssist.Features.Shell;

namespace StreamAssist.Features.Keyboard;



/// <summary>
/// Drives the KeyboardTab view. Composition root for the keyboard feature -
/// owns the shared InputManager/KeyboardFeatureSettings and hands each off
/// to the TopMenu and KeyboardDisplay child viewmodels.
/// </summary>
public partial class KeyboardTabViewModel : ViewModelBase
{
    public TopMenuViewModel TopMenu { get; }
    public KeyboardDisplayViewModel KeyboardDisplay { get; }



    // constructor



    public KeyboardTabViewModel(InputManager inputManager, KeyboardFeatureSettings settings)
    {
        TopMenu = new TopMenuViewModel(inputManager, settings);
        KeyboardDisplay = new KeyboardDisplayViewModel(inputManager);
    }
}
