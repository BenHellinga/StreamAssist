using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Core.Connections;
using StreamAssist.Shell;

namespace StreamAssist.Components.Audio.Conditions;



// whether Voicemeeter is connected (the Connections tab), e.g. an If around Voicemeeter blocks so they're skipped while it isn't
// instead of failing
public class VoicemeeterConnectedCondition : ConditionBlock
{
    // info



    [JsonIgnore] public override string Name => "Voicemeeter Connected";
    [JsonIgnore] public override string Description => "True while Voicemeeter is connected (the Connections tab), e.g. an If around Voicemeeter blocks so they're skipped while it isn't, instead of failing.";



    // check



    protected override Task<bool> CheckAsync(TriggerContext context) =>
        Task.FromResult(GlobalContext.VoicemeeterManager?.Status == ConnectionStatus.Connected);
}
