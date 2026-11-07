using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Widgets.Model;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Widgets.UI;



// the Widgets tab's widget list: a card per kind of widget (Mic Bounce, Wheel), dragged into the widgets to add one,
// filtered by the search box (name or description)
public partial class WidgetPaletteViewModel : ViewModelBase, IListSearch
{
    // private



    [ObservableProperty] private string _searchText;
    [ObservableProperty] private string _selectedTag;



    // public



    public IReadOnlyList<WidgetCardViewModel> Cards { get; }
    public IReadOnlyList<string> Tags { get; } = [SlotTags.All, "Widget"]; // its one kind, like the block lists' type filter
    public PaletteSectionViewModel Section { get; } // its one heading, like the block list's



    // constructor



    public WidgetPaletteViewModel()
    {
        Cards = Enum.GetValues<WidgetKind>().Select(kind => new WidgetCardViewModel(kind)).ToList();
        _searchText = "";
        _selectedTag = SlotTags.All;
        Section = new("Widgets", "Widgets", Cards);
    }



    // ui events



    partial void OnSearchTextChanged(string value)
    {
        var search = value.Trim();
        Section.IsSearching = search != "";
        Section.Cards = search == "" ? Cards : Cards.Where(c =>
            c.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || c.Description.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
    }



    // disposal



    public override void Dispose() => Section.Dispose();
}
