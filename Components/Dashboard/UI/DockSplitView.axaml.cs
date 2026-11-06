using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;

namespace StreamAssist.Components.Dashboard.UI;



public partial class DockSplitView : UserControl
{
    private const double MinRegion = 60; // a region can't be dragged smaller than this



    public DockSplitView()
    {
        InitializeComponent();
    }



    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Build();
    }



    // region, splitter, region, ...: each region gets its share as a star size, each splitter an Auto column (its style
    // gives it its width and the LineGap either side)
    // (or row) with the window's splitter margins, so it looks like the panels' splitters (a thick line says it drags)
    private void Build()
    {
        SplitGrid.Children.Clear();
        SplitGrid.ColumnDefinitions.Clear();
        SplitGrid.RowDefinitions.Clear();
        if (DataContext is not DockSplitViewModel vm) return;

        var horizontal = vm.IsHorizontal;

        for (var i = 0; i < vm.Children.Count; i++)
        {
            if (i > 0)
            {
                var splitter = new GridSplitter { ResizeDirection = horizontal ? GridResizeDirection.Columns : GridResizeDirection.Rows };
                splitter.Classes.Add(horizontal ? "ColumnSplitter" : "RowSplitter");
                splitter.DragCompleted += (_, _) => vm.SetSizes(RegionSizes(horizontal));
                AddCell(splitter, GridLength.Auto, null, horizontal);
            }

            AddCell(new ContentControl { Content = vm.Children[i] }, new GridLength(vm.Sizes[i], GridUnitType.Star), MinRegion, horizontal);
        }
    }



    private void AddCell(Control control, GridLength length, double? min, bool horizontal)
    {
        if (horizontal)
        {
            SplitGrid.ColumnDefinitions.Add(new ColumnDefinition(length) { MinWidth = min ?? 0 });
            Grid.SetColumn(control, SplitGrid.ColumnDefinitions.Count - 1);
        }
        else
        {
            SplitGrid.RowDefinitions.Add(new RowDefinition(length) { MinHeight = min ?? 0 });
            Grid.SetRow(control, SplitGrid.RowDefinitions.Count - 1);
        }

        SplitGrid.Children.Add(control);
    }



    // the regions' current sizes (every other column or row, the ones between are splitters)
    private List<double> RegionSizes(bool horizontal) => horizontal
        ? SplitGrid.ColumnDefinitions.Where((_, i) => i % 2 == 0).Select(c => c.ActualWidth).ToList()
        : SplitGrid.RowDefinitions.Where((_, i) => i % 2 == 0).Select(r => r.ActualHeight).ToList();
}
