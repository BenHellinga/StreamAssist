using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Input.Background;
using StreamAssist.Components.Input.Model;
using StreamAssist.Components.Input.UI;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Input.Conditions;



public class KeyPressedCondition : ConditionBlock
{
    // info



    [JsonIgnore] public override string Name => "Key Pressed";
    [JsonIgnore] public override string Description => "True while the key is held down on the chosen keyboard (left and right shift/ctrl/alt count as the same key).";



    // constants



    private const string AnyKeyboard = "Any keyboard";



    // private



    private readonly MenuSlot _keyboard;
    private readonly IReadOnlyList<BlockSlotBase> _slots;
    private string _key;



    // public



    [ConfigurableParameter("Key", ConfiguratorType.Text)] [JsonPropertyName("key")]
    public string Key
    {
        get => _key;
        set => SetParameter(ref _key, value);
    }



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("keyboard")]
    private ValueSlotData JsonKeyboard
    {
        get => _keyboard.GetData();
        set => _keyboard.LoadData(value);
    }



    // constructor



    public KeyPressedCondition()
    {
        _keyboard = new("Keyboard", GetKeyboardMenuItems, AnyKeyboard, isFixed: false);
        _key = "";
        _slots = [_keyboard];

        TrackSlots();
    }



    // public



    // keyboards are saved by their display label (the same text the keyboard slot dropdown shows)
    public static List<string> GetKeyboardMenuItems()
    {
        var items = new List<string> { AnyKeyboard };
        items.AddRange(DeviceInputManager.Devices.Where(d => d.Kind == DeviceKind.Keyboard).Select(d => d.GetDisplayLabel()));
        return items;
    }



    // check



    protected override async Task<bool> CheckAsync(TriggerContext context)
    {
        var keyboard = await _keyboard.EvaluateAsync(context);
        var inputManager = GlobalContext.DeviceInputManager;
        if (inputManager == null)
        {
            AppLog.WriteError($"{Name}: keyboard input not set up");
            return false;
        }

        if (!KeyNames.TryParse(_key, out var vk))
        {
            AppLog.WriteError($"{Name}: '{_key}' isn't a key");
            return false;
        }

        if (keyboard == AnyKeyboard)
            return inputManager.IsKeyDown(vk);

        var device = DeviceInputManager.Devices.FirstOrDefault(d => d.Kind == DeviceKind.Keyboard && d.GetDisplayLabel() == keyboard);
        if (device == null)
        {
            AppLog.WriteWarning($"{Name}: keyboard '{keyboard}' not connected");
            return false;
        }

        return inputManager.IsKeyDown(device.DevicePath, vk);
    }
}
