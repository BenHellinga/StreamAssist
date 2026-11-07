using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Blocks.Actions;



// switches a tab to another of its configs, the same as picking it in the tab's config bar (e.g. a key that swaps the
// Input tab to a "BRB" config, or a chat command that turns on an April Fools chat tree)
public class ChangeConfigAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Change Config";
    [JsonIgnore] public override string Description => "Switches a tab to its config with that name (case ignored), the same as picking it in the tab's config bar. Already in use: nothing happens.";



    // private



    private readonly MenuSlot _tab;
    private readonly ValueSlot<string> _config;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetTabMenuItems() => ["Input", "Chat", "Events", "Dashboard", "Themes"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("tab")]
    private ValueSlotData JsonTab
    {
        get => _tab.GetData();
        set => _tab.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("config")]
    private ValueSlotData JsonConfig
    {
        get => _config.GetData();
        set => _config.LoadData(value);
    }



    // constructor



    public ChangeConfigAction()
    {
        _tab = new("Tab", GetTabMenuItems, "Input");
        _config = new("Config");
        _slots = [_tab, _config];

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var tabOption = await _tab.EvaluateAsync(context);
        var configs = ConfigsOf(tabOption) ?? throw Fail($"no {tabOption} tab");
        var name = (await _config.EvaluateAsync(context)).Trim();

        if (!configs.Names.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
            throw Fail($"no {tabOption} config '{name}'");

        configs.TrySelect(name);
    }



    // helpers



    // the tab's configs (null if the app hasn't made that tab's model, e.g. in the tests)
    private static IConfigSet? ConfigsOf(string tab) => tab switch
    {
        "Input" => GlobalContext.InputComponentModel?.ListModel.Configs,
        "Chat" => GlobalContext.ChatbotComponentModel?.ChatConfigs,
        "Events" => GlobalContext.ChatbotComponentModel?.EventsConfigs,
        "Dashboard" => GlobalContext.DashboardComponentModel?.Configs,
        "Themes" => GlobalContext.ThemeComponentModel?.Configs,
        _ => null,
    };
}
