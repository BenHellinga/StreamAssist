using System.Collections.Generic;
using StreamAssist.Components.Dashboard.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Dashboard.UI;



// a region split into side by side (or stacked) regions, each a DockViewModel or another split, with splitters between
// them. Dragging a splitter saves the new shares in the model (the layout keeps its shape, nothing is rebuilt)
public class DockSplitViewModel : ViewModelBase
{
    // private



    private readonly DashboardConfigModel _config;
    private readonly DockLayoutNode _node;
    private readonly IReadOnlyList<ViewModelBase> _children;



    // public



    public bool IsHorizontal => _node.Direction == SplitDirection.Horizontal;
    public IReadOnlyList<ViewModelBase> Children => _children;
    public IReadOnlyList<double> Sizes => _node.Sizes;



    // constructor



    public DockSplitViewModel(DashboardConfigModel config, DockLayoutNode node, IReadOnlyList<ViewModelBase> children)
    {
        _config = config;
        _node = node;
        _children = children;
    }



    // public api



    // the regions' sizes after a splitter was let go, in pixels (only their shares are kept)
    public void SetSizes(IReadOnlyList<double> sizes) => _config.SetSizes(_node, sizes);



    // disposal



    public override void Dispose()
    {
        foreach (var child in _children) child.Dispose();
    }
}
