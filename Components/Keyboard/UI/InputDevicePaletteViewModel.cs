using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Keyboard.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Keyboard.UI;



// the Keyboard tab's input device list: a card per kind of input device (Keyboard, Generic), dragged into the keyboards
// to add one, filtered by the search box (name or description)
public partial class InputDevicePaletteViewModel : ViewModelBase
{
    // private



    [ObservableProperty] private string _searchText;
    [ObservableProperty] private IReadOnlyList<InputDeviceCardViewModel> _shown;



    // public



    public IReadOnlyList<InputDeviceCardViewModel> Cards { get; }



    // constructor



    public InputDevicePaletteViewModel()
    {
        Cards = KeyboardSlotModel.Layouts.Select(layout => new InputDeviceCardViewModel(layout)).ToList();
        _searchText = "";
        _shown = Cards;
    }



    // ui events



    partial void OnSearchTextChanged(string value)
    {
        var search = value.Trim();
        Shown = search == "" ? Cards : Cards.Where(c =>
            c.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || c.Description.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
    }



    // disposal



    public override void Dispose() { }
}
