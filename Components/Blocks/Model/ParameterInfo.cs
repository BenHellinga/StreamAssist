namespace StreamAssist.Components.Blocks.Model;



// one parameter a trigger passes to its binding (e.g. viewers on a raid), shown in the editor's Parameters list
// the value is always text when it arrives, Kind says what it can be read as (Get Parameter's "As")
public sealed record ParameterInfo(string Name, ValueKind Kind, string Description);
