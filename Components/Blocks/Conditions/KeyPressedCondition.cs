using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Keyboard.Background;
using StreamAssist.Components.Keyboard.UI;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Blocks.Conditions;



public class KeyPressedCondition : ConditionBlock
{
    // info



    [JsonIgnore] public override string Name => "Key Pressed";
    [JsonIgnore] public override string Description => "True while the key is held down on the chosen keyboard (left and right shift/ctrl/alt count as the same key).";



    // constants



    private const string AnyKeyboard = "Any keyboard";



    // private



    private string _keyboard;
    private string _key;



    // public



    [ConfigurableParameter("Keyboard", ConfiguratorType.Menu)] [JsonPropertyName("keyboard")]
    public string Keyboard
    {
        get => _keyboard;
        set => SetParameter(ref _keyboard, value);
    }

    [ConfigurableParameter("Key", ConfiguratorType.Text)] [JsonPropertyName("key")]
    public string Key
    {
        get => _key;
        set => SetParameter(ref _key, value);
    }



    // constructor



    public KeyPressedCondition()
    {
        _keyboard = AnyKeyboard;
        _key = "";
    }



    // public



    // keyboards are saved by their display label (the same text the keyboard slot dropdown shows)
    public static List<string> GetKeyboardMenuItems()
    {
        var items = new List<string> { AnyKeyboard };
        items.AddRange(KeyboardInputManager.Devices.Select(d => d.GetDisplayLabel()));
        return items;
    }



    // check



    protected override Task<bool> CheckAsync(TriggerContext context)
    {
        var inputManager = GlobalContext.KeyboardInputManager;
        if (inputManager == null)
        {
            AppLog.WriteError("Could not check key - no keyboard input in global context");
            return Task.FromResult(false);
        }

        if (!DeviceDisplayHelper.TryParseKeyName(_key, out var vk))
        {
            AppLog.WriteError($"Could not check key - '{_key}' is not a key name");
            return Task.FromResult(false);
        }

        if (_keyboard == AnyKeyboard)
            return Task.FromResult(inputManager.IsKeyDown(vk));

        var device = KeyboardInputManager.Devices.FirstOrDefault(d => d.GetDisplayLabel() == _keyboard);
        if (device == null)
        {
            AppLog.WriteWarning($"Could not check key - keyboard '{_keyboard}' is not connected");
            return Task.FromResult(false);
        }

        return Task.FromResult(inputManager.IsKeyDown(device.DevicePath, vk));
    }
}
