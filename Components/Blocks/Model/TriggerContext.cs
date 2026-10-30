using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace StreamAssist.Components.Blocks.Model;



// Start/Stop come from things that are held (key down/up), Once from instant triggers (chat command, timer)
public enum TriggerPhase { Start, Stop, Once }



// passed down the block tree on every run, each run gets its own (so two runs at once never share data)
public class TriggerContext
{
    // private



    private readonly Dictionary<string, string> _parameters;



    // public



    public TriggerPhase Phase { get; }
    public CancellationToken CancellationToken { get; }

    // named values from whatever triggered this run, e.g. a chat command's parameters, or "user" / "message",
    // plus any a Set Parameter block added earlier in this run
    public IReadOnlyDictionary<string, string> Parameters => _parameters;



    // constructor



    // the parameters are copied, so setting one during this run never changes the caller's (or another run's)
    public TriggerContext(TriggerPhase phase, CancellationToken cancellationToken, IReadOnlyDictionary<string, string>? parameters = null)
    {
        Phase = phase;
        CancellationToken = cancellationToken;
        _parameters = parameters == null ? [] : new Dictionary<string, string>(parameters);
    }



    // public api



    // adds or replaces a parameter for the rest of this run (Set Parameter), e.g. ("otherCost", "100")
    public void SetParameter(string name, string value) => _parameters[name] = value;



    // a name {name} can use: letters, digits and _
    public static bool IsValidName(string name) => Regex.IsMatch(name, @"^\w+$");



    // fills in {name} with the parameter's value, e.g. "{user} is lurking", unknown names are left as they are
    public string Format(string template) =>
        Regex.Replace(template, @"\{(\w+)\}", m => Parameters.TryGetValue(m.Groups[1].Value, out var value) ? value : m.Value);
}
