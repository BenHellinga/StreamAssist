using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Values;



public class GetParameterBlock : AdaptableValueBlock
{
    // info



    [JsonIgnore] public override string Name => "Get Parameter";
    [JsonIgnore] public override string Description => "A parameter from whatever triggered this (the editor's Parameters list shows which there are), read as text, a number or true/false. Typed text can also use {name}.";



    // private



    private string _parameterName;



    // public



    [ConfigurableParameter("Name", ConfiguratorType.Text)] [JsonPropertyName("parameterName")]
    public string ParameterName
    {
        get => _parameterName;
        set => SetParameter(ref _parameterName, value);
    }

    // what it's read as, placing it in a slot picks the slot's type
    [ConfigurableParameter("As", ConfiguratorType.Menu)] [JsonPropertyName("kind")]
    public string As
    {
        get => KindName;
        set => KindName = value;
    }

    public static List<string> GetAsMenuItems() => ["Text", "Int", "Double", "Bool"];

    [JsonIgnore] public override IReadOnlyList<ValueKind> AllowedKinds => [ValueKind.Text, ValueKind.Int, ValueKind.Double, ValueKind.Bool];



    // constructor



    public GetParameterBlock() : base(ValueKind.Text)
    {
        _parameterName = "";
    }



    // compute



    // parameters are always text, it's read as the chosen type here, where the error can say which parameter it was
    protected override Task<object> ComputeAsync(TriggerContext context)
    {
        var name = _parameterName.Trim();
        if (!context.Parameters.TryGetValue(name, out var text))
            throw Fail($"there's no parameter '{name}' (keys have no parameters, chat commands and events do)");

        if (!ValueTypes.TryParse(text, Kind, out var value))
            throw Fail($"parameter '{name}' is '{text}', not {ValueTypes.Describe(Kind)}");

        return Task.FromResult(value);
    }
}
