using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Actions;



public class StopAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Stop";
    [JsonIgnore] public override string Description => "Ends this run: nothing after it runs, in this block or any block around it (e.g. If not a moderator, reply, then Stop).";



    // private



    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // constructor



    public StopAction()
    {
        _slots = [];

        TrackSlots();
    }



    // execute



    // the root ends the run quietly, it isn't an error
    protected override Task ExecuteAsync(TriggerContext context) => throw new StopRunException();
}
