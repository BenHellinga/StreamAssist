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



    // private



    private string _variable;
    private readonly ValueSlot<double> _amount;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Variable", ConfiguratorType.Menu)] [JsonPropertyName("variable")]
    public string Variable
    {
        get => _variable;
        set
        {
            SetParameter(ref _variable, value);
        }
    }

    public static List<string> GetVariableMenuItems() => GlobalContext.DataModel?.VariableNames() ?? [];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("amount")]
    private ValueSlotData JsonAmount
    {
        get => _amount.GetData();
        set => _amount.LoadData(value);
    }



    // constructor



    public ChangeVariableAction()
    {
        _variable = "";
        _amount = new("Amount");
        _slots = [_amount];

        _amount.SetText("1");

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var data = GlobalContext.DataModel ?? throw Fail("data not set up");
        var amount = await _amount.EvaluateAsync(context);

        if (!data.TryChangeVariable(_variable, amount, out _)) throw new BlockFailedException("variable not changed");
    }
}
