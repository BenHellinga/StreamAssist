using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Data.Values;



public class GetVariableBlock : AdaptableValueBlock
{
    // info



    [JsonIgnore] public override string Name => "Get Variable";
    [JsonIgnore] public override string Description => "A variable's value (made in the Data tab, kept after the app closes). Picking a variable picks its type. In typed text, {var.name} works too.";
    [JsonIgnore] public override BlockGroup Group => BlockGroup.Variables;



    // private



    private readonly MenuSlot _variable;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetVariableMenuItems() => GlobalContext.DataModel?.VariableNames() ?? [];

    // what it's read as, placing it in a slot picks the slot's type (saved after the setting, so a loaded block keeps it)
    [ConfigurableParameter("As", ConfiguratorType.Menu)] [JsonPropertyName("kind")]
    public string As
    {
        get => KindName;
        set => KindName = value;
    }

    public static List<string> GetAsMenuItems() => ["Text", "Int", "Double", "Bool"];

    [JsonIgnore] public override IReadOnlyList<ValueKind> AllowedKinds => [ValueKind.Text, ValueKind.Int, ValueKind.Double, ValueKind.Bool];
    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // menu events



    // picking an option in the dropdown (not a block in it: that's only known when it runs)
    private void OnVariablePicked()
    {
        var value = _variable.Text;
        if (GlobalContext.DataModel?.VariableKind(value) is { } kind && Kind != kind) TrySetKind(kind);
    }



    // json



    [JsonInclude] [JsonPropertyName("variable")]
    private ValueSlotData JsonVariable
    {
        get => _variable.GetData();
        set => _variable.LoadData(value);
    }



    // constructor



    public GetVariableBlock() : base(ValueKind.Text)
    {
        _variable = new("Variable", GetVariableMenuItems, "", isFixed: false);
        _slots = [_variable];

        _variable.TextChanged += OnVariablePicked;
        TrackSlots();
    }



    // compute



    protected override async Task<object> ComputeAsync(TriggerContext context)
    {
        var variable = await _variable.EvaluateAsync(context);
        var data = GlobalContext.DataModel ?? throw Fail("data not set up");
        if (!data.TryGetVariable(variable, out var kind, out var value)) throw new BlockFailedException("no variable");

        if (!ValueTypes.TryConvert(value, kind, Kind, out var result, out var error)) throw Fail(error);
        return result;
    }
}
