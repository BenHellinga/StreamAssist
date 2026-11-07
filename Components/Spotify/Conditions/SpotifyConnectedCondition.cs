using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Core.Connections;
using StreamAssist.Shell;

namespace StreamAssist.Components.Spotify.Conditions;



// whether Spotify is connected (the Connections tab), e.g. an If around Spotify blocks so they're skipped while it isn't
// instead of failing
public class SpotifyConnectedCondition : ConditionBlock
{
    // info



    [JsonIgnore] public override string Name => "Spotify Connected";
    [JsonIgnore] public override string Description => "True while Spotify is connected (the Connections tab), e.g. an If around Spotify blocks so they're skipped while it isn't, instead of failing.";



    // check



    protected override Task<bool> CheckAsync(TriggerContext context) =>
        Task.FromResult(GlobalContext.SpotifyManager?.Status == ConnectionStatus.Connected);
}
