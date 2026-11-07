using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Core.Connections;
using StreamAssist.Shell;

namespace StreamAssist.Components.OBS.Conditions;



// whether OBS is connected (the Connections tab), e.g. an If around OBS blocks so they're skipped while it isn't
// instead of failing
public class ObsConnectedCondition : ConditionBlock
{
    // info



    [JsonIgnore] public override string Name => "OBS Connected";
    [JsonIgnore] public override string Description => "True while OBS is connected (the Connections tab), e.g. an If around OBS blocks so they're skipped while it isn't, instead of failing.";



    // check



    protected override Task<bool> CheckAsync(TriggerContext context) =>
        Task.FromResult(GlobalContext.ObsManager?.Status == ConnectionStatus.Connected);
}
