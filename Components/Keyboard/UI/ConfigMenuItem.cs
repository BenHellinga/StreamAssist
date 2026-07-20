using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Components.Keyboard.UI;



// data class for a single config menu item
public class ConfigMenuItem
{
    // private



    private string _label;
    private KeybindConfigModel? _config;



    // public



    public string Label => _label;
    public KeybindConfigModel? Model => _config;



    // constructor



    public ConfigMenuItem(string label, KeybindConfigModel? config)
    {
        _label = label;
        _config = config;
    }
}