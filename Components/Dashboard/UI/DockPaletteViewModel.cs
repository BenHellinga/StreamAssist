using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Dashboard.Model;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Dashboard.UI;



// the Dashboard tab's dock list: one card per kind of dock, dragged onto the dashboard to add one, filtered by the
// search box (name or description). Hidden with no config (there's nowhere to put them)
public partial class DockPaletteViewModel : ViewModelBase, IListSearch
{
    // private



    private readonly DashboardComponentModel _model;

    [ObservableProperty] private bool _hasConfig;
    [ObservableProperty] private string _searchText;
    [ObservableProperty] private string _selectedTag;



    // public



    public IReadOnlyList<DockCardViewModel> Cards { get; }
    public IReadOnlyList<string> Tags { get; } = [SlotTags.All, "Dock"]; // its one kind, like the block lists' type filter
    public PaletteSectionViewModel Section { get; } // its one heading, like the block list's



    // constructor



    public DockPaletteViewModel(DashboardComponentModel model)
    {
        _model = model;
        _hasConfig = _model.Config != null;
        Cards = Enum.GetValues<DockKind>().Select(k => new DockCardViewModel(k)).ToList();
        _searchText = "";
        _selectedTag = SlotTags.All;
        Section = new("Docks", "Docks", Cards);

        _model.Configs.ListChanged += OnConfigsChanged;
    }



    // ui events



    partial void OnSearchTextChanged(string value)
    {
        var search = value.Trim();
        Section.IsSearching = search != "";
        Section.Cards = search == "" ? Cards : Cards.Where(c =>
            c.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || c.Description.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
    }



    // model events



    private void OnConfigsChanged() => HasConfig = _model.Config != null;



    // disposal



    public override void Dispose()
    {
        _model.Configs.ListChanged -= OnConfigsChanged;
        Section.Dispose();
    }
}
