using System.Threading.Tasks;
using StreamAssist.Components.Keyboard.Model;
using StreamAssist.Components.AppAction.Model;
using System.Text.Json.Serialization;

namespace StreamAssist.AppActions;



// TODO redo with global context
public class ChangeKeyboardSlotConfigAction : ActionModel
{
    public static KeyboardComponentModel? Model;
    [JsonIgnore] public override string Name => "Change Config";
    [JsonIgnore] public override string Description => "";



    public override Task SafeExecuteAsync()
    {
        if (Model == null) return Task.CompletedTask;
        if (Model.ListModel.Slots.Count == 0) return Task.CompletedTask;

        Model.ListModel.Slots[0].TrySetSelectedConfigByString("Changed");

        return Task.CompletedTask;
    }
}