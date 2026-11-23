using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Scripting;

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



    public static List<string> GetTabMenuItems() => ["Input", "Chat", "Events", "Dashboard", "Scripts", "Themes"];

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
        var tab = await _tab.EvaluateAsync(context);
        var name = await _config.EvaluateAsync(context);
        if (!Enum.TryParse<ConfigTab>(tab, out var configTab)) throw Fail($"no {tab} tab");
        Api(context).App.SetConfig(configTab, name);
    }
}
