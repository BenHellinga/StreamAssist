using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Input.Model;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Input.UI;



// the Input tab's input device list: a card per kind of input device (Keyboard, Generic), dragged into the keyboards
// to add one, filtered by the search box (name or description)
public partial class InputDevicePaletteViewModel : ViewModelBase, IListSearch
{
    // private



    [ObservableProperty] private string _searchText;
    [ObservableProperty] private string _selectedTag;



    // public



    public IReadOnlyList<InputDeviceCardViewModel> Cards { get; }
    public IReadOnlyList<string> Tags { get; } = [SlotTags.All, "Input Device"]; // its one kind, like the block lists' type filter
    public PaletteSectionViewModel Section { get; } // its one heading, like the block list's



    // constructor



    public InputDevicePaletteViewModel()
    {
        Cards = InputSlotModel.Layouts.Select(layout => new InputDeviceCardViewModel(layout)).ToList();
        _searchText = "";
        _selectedTag = SlotTags.All;
        Section = new("Input Devices", "InputDevices", Cards);
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
