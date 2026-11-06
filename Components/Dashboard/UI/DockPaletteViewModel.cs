using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Dashboard.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Dashboard.UI;



// the Dashboard tab's dock list: one card per kind of dock, dragged onto the dashboard to add one, filtered by the
// search box (name or description). Hidden with no config (there's nowhere to put them)
public partial class DockPaletteViewModel : ViewModelBase
{
    // private



    private readonly DashboardComponentModel _model;

    [ObservableProperty] private bool _hasConfig;
    [ObservableProperty] private string _searchText;
    [ObservableProperty] private IReadOnlyList<DockCardViewModel> _shown;



    // public



    public IReadOnlyList<DockCardViewModel> Cards { get; }



    // constructor



    public DockPaletteViewModel(DashboardComponentModel model)
    {
        _model = model;
        _hasConfig = _model.Config != null;
        Cards = Enum.GetValues<DockKind>().Select(k => new DockCardViewModel(k)).ToList();
        _searchText = "";
        _shown = Cards;

        _model.Configs.ListChanged += OnConfigsChanged;
    }



    // ui events



    partial void OnSearchTextChanged(string value)
    {
        var search = value.Trim();
        Shown = search == "" ? Cards : Cards.Where(c =>
            c.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || c.Description.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
    }



    // model events



    private void OnConfigsChanged() => HasConfig = _model.Config != null;



    // disposal



    public override void Dispose()
    {
        _model.Configs.ListChanged -= OnConfigsChanged;
    }
}
