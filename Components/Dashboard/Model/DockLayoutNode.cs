using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace StreamAssist.Components.Dashboard.Model;



// side by side (a row of regions) or stacked (a column of them), saved by name
[JsonConverter(typeof(JsonStringEnumConverter<SplitDirection>))]
public enum SplitDirection { Horizontal, Vertical }



// which side of a dock another one is dropped on
public enum DockEdge { Left, Right, Top, Bottom }



// one region of the dashboard: either a single dock (DockId), or a split into two or more regions with splitters
// between them (Sizes are their shares of the space, e.g. 1, 2 = a third and two thirds). Only DashboardConfigModel
// changes it, and it keeps it tidy: a split always has two or more children, never a child split the same way
public sealed class DockLayoutNode
{
    [JsonPropertyName("dock")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DockId { get; set; }

    [JsonPropertyName("direction")]
    public SplitDirection Direction { get; set; }

    [JsonPropertyName("children")]
    public List<DockLayoutNode> Children { get; set; } = [];

    [JsonPropertyName("sizes")]
    public List<double> Sizes { get; set; } = [];

    [JsonIgnore] public bool IsDock => DockId != null;



    public static DockLayoutNode ForDock(string dockId) => new() { DockId = dockId };



    public static DockLayoutNode Split(SplitDirection direction, params DockLayoutNode[] children) =>
        new() { Direction = direction, Children = [.. children], Sizes = children.Select(_ => 1.0).ToList() };
}
