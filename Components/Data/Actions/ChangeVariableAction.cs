using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Data.Actions;



public class ChangeVariableAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Change Variable";
    [JsonIgnore] public override string Description => "Adds Amount to a number variable (negative takes away, e.g. a death counter), or flips a true/false one. Done in one step, so two runs at once can't lose a change the way Get then Set could.";
    [JsonIgnore] public override BlockGroup Group => BlockGroup.Variables;



    // private



    private readonly MenuSlot _variable;
    private readonly ValueSlot<double> _amount;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetVariableMenuItems() => GlobalContext.DataModel?.VariableNames() ?? [];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("variable")]
    private ValueSlotData JsonVariable
    {
        get => _variable.GetData();
        set => _variable.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("amount")]
    private ValueSlotData JsonAmount
    {
        get => _amount.GetData();
        set => _amount.LoadData(value);
    }



    // constructor



    public ChangeVariableAction()
    {
        _variable = new("Variable", GetVariableMenuItems, "", isFixed: false);
        _amount = new("Amount");
        _slots = [_variable, _amount];

        _amount.SetText("1");

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var variable = await _variable.EvaluateAsync(context);
        var data = GlobalContext.DataModel ?? throw Fail("data not set up");
        var amount = await _amount.EvaluateAsync(context);

        if (!data.TryChangeVariable(variable, amount, out _)) throw new BlockFailedException("variable not changed");
    }
}
