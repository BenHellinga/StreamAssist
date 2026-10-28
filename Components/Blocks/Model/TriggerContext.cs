using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace StreamAssist.Components.Blocks.Model;



// Start/Stop come from things that are held (key down/up), Once from instant triggers (chat command, timer)
public enum TriggerPhase { Start, Stop, Once }



// passed down the block tree on every run, each run gets its own (so two runs at once never share data)
public class TriggerContext(TriggerPhase phase, CancellationToken cancellationToken, IReadOnlyDictionary<string, string>? parameters = null)
{
    private static readonly IReadOnlyDictionary<string, string> NoParameters = new Dictionary<string, string>();

    public TriggerPhase Phase { get; } = phase;
    public CancellationToken CancellationToken { get; } = cancellationToken;

    // named values from whatever triggered this run, e.g. a chat command's parameters, or "user" / "message"
    public IReadOnlyDictionary<string, string> Parameters { get; } = parameters ?? NoParameters;



    // fills in {name} with the parameter's value, e.g. "{user} is lurking", unknown names are left as they are
    public string Format(string template) =>
        Regex.Replace(template, @"\{(\w+)\}", m => Parameters.TryGetValue(m.Groups[1].Value, out var value) ? value : m.Value);
}
