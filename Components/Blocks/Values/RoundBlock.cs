using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Values;



public class RoundBlock : ValueBlock<int>
{
    // info



    [JsonIgnore] public override string Name => "Round";
    [JsonIgnore] public override string Description => "Turns a number with decimals into a whole number: Round (nearest, .5 goes up), Floor (down), Ceiling (up) or Truncate (drops the decimals).";



    // private



    private string _mode;
    private readonly ValueSlot<double> _number;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Mode", ConfiguratorType.Menu)] [JsonPropertyName("mode")]
    public string Mode
    {
        get => _mode;
        set => SetParameter(ref _mode, value);
    }

    public static List<string> GetModeMenuItems() => ["Round", "Floor", "Ceiling", "Truncate"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("number")]
    private ValueSlotData JsonNumber
    {
        get => _number.GetData();
        set => _number.LoadData(value);
    }



    // constructor



    public RoundBlock()
    {
        _mode = "Round";
        _number = new("Number");
        _slots = [_number];

        TrackSlots();
    }



    // compute



    protected override async Task<int> ComputeAsync(TriggerContext context)
    {
        var number = await _number.EvaluateAsync(context);

        var rounded = _mode switch
        {
            "Floor" => Math.Floor(number),
            "Ceiling" => Math.Ceiling(number),
            "Truncate" => Math.Truncate(number),
            _ => Math.Round(number, MidpointRounding.AwayFromZero),
        };

        if (double.IsNaN(rounded) || rounded < int.MinValue || rounded > int.MaxValue)
            throw Fail($"{ValueTypes.ToText(number)} is too big for a whole number");

        return (int)rounded;
    }
}
