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



    // private



    private string _variable;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Variable", ConfiguratorType.Menu)] [JsonPropertyName("variable")]
    public string Variable
    {
        get => _variable;
        set
        {
            SetParameter(ref _variable, value);
            if (GlobalContext.DataModel?.VariableKind(value) is { } kind && Kind != kind) TrySetKind(kind);
        }
    }

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



    // constructor



    public GetVariableBlock() : base(ValueKind.Text)
    {
        _variable = "";
        _slots = [];

        TrackSlots();
    }



    // compute



    protected override Task<object> ComputeAsync(TriggerContext context)
    {
        var data = GlobalContext.DataModel ?? throw Fail("data not set up");
        if (!data.TryGetVariable(_variable, out var kind, out var value)) throw new BlockFailedException("no variable");

        if (!ValueTypes.TryConvert(value, kind, Kind, out var result, out var error)) throw Fail(error);
        return Task.FromResult(result);
    }
}
