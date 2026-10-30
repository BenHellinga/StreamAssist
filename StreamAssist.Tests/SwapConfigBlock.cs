using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Tests;

public class SwapConfigBlock : ActionBlock
{
    [JsonIgnore] public override string Name => "Swap";
    [JsonIgnore] public override string Description => "test";
    [JsonIgnore] public KeyboardSlotModel? Slot { get; set; }
    [JsonIgnore] public KeybindConfigModel? To { get; set; }
    protected override Task ExecuteAsync(TriggerContext context) { Slot!.TrySetSelectedConfig(To); return Task.CompletedTask; }
}
