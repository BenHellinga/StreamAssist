using System;
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
    private readonly Dictionary<string, string> _locals; // a String Builder's rows, only while its text is worked out



    // public



    public TriggerPhase Phase { get; }
    public CancellationToken CancellationToken { get; }

    // named values from whatever triggered this run, e.g. a chat command's parameters, or "user" / "message",
    // plus any a Set Parameter block added earlier in this run
    public IReadOnlyDictionary<string, string> Parameters => _parameters;

    // the block-scoped names (a String Builder's rows), empty for a run's own context
    public IReadOnlyDictionary<string, string> Locals => _locals;



    // constructor



    // the parameters are copied, so setting one during this run never changes the caller's (or another run's)
    public TriggerContext(TriggerPhase phase, CancellationToken cancellationToken, IReadOnlyDictionary<string, string>? parameters = null)
    {
        Phase = phase;
        CancellationToken = cancellationToken;
        _parameters = parameters == null ? [] : new Dictionary<string, string>(parameters);
        _locals = [];
    }



    // the same run with a block's own names on top (String Builder): the run's parameters are shared, the locals are
    // a copy of any outer block's plus what this block adds with SetLocal, and nothing after the block sees them
    private TriggerContext(TriggerContext outer)
    {
        Phase = outer.Phase;
        CancellationToken = outer.CancellationToken;
        _parameters = outer._parameters;
        _locals = new Dictionary<string, string>(outer._locals);
    }



    // public api



    // adds or replaces a parameter for the rest of this run (Set Parameter), e.g. ("otherCost", "100")
    public void SetParameter(string name, string value) => _parameters[name] = value;



    public TriggerContext WithLocals() => new(this);



    // adds or replaces a block-scoped name (only on a context from WithLocals)
    public void SetLocal(string name, string value) => _locals[name] = value;



    // a name {name} can use: letters, digits and _
    public static bool IsValidName(string name) => Regex.IsMatch(name, @"^\w+$");



    // how {var.name} reads a variable (set by the app at startup, the Data component owns variables), null if there's none
    public static Func<string, string?>? VariableText { get; set; }



    // fills in each {name}, looking in the closest scope first: a String Builder's own rows (loc), then the run's
    // parameters (run), then the variables (var), e.g. "{user} is lurking", "{deaths} deaths". A prefix reads only that
    // scope: {loc.name}, {run.name}, {var.name}. Unknown names are left as they are
    public string Format(string template) =>
        Regex.Replace(template, @"\{(?:(loc|run|var)\.)?(\w+)\}", m =>
        {
            var scope = m.Groups[1].Value;
            var name = m.Groups[2].Value;
            if (scope is "" or "loc" && _locals.TryGetValue(name, out var local)) return local;
            if (scope is "" or "run" && _parameters.TryGetValue(name, out var value)) return value;
            if (scope is "" or "var") return VariableText?.Invoke(name) ?? m.Value;
            return m.Value;
        });
}
