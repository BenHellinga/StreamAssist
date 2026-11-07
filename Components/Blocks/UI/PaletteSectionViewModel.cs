using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Core.Mvvm;
using StreamAssist.Shell;

namespace StreamAssist.Components.Blocks.UI;



// a heading in a card list that isn't the block list (the dock list's "Docks", the input device list's "Input
// Devices"), drawn and collapsed like a block list heading: its title (with how many cards match while searching) and
// an arrow that collapses / expands it, kept in window.json with the block list's headings
public sealed partial class PaletteSectionViewModel : ViewModelBase
{
    // private



    private readonly string _key; // its name in window.json
    private readonly WindowLayoutModel? _layout; // null in the tests: then it's only kept here

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HeadingText))] private IReadOnlyList<ViewModelBase> _cards; // the ones matching the search
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Arrow), nameof(ShowCards))] private bool _isCollapsed;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HeadingText))] private bool _isSearching;



    // public



    public string Title { get; }
    public bool ShowCards => !IsCollapsed;
    public string HeadingText => $"{Title} ({Cards.Count})"; // with how many it shows (all, or the ones matching)
    public string Arrow => IsCollapsed ? "▸" : "▾";



    // constructor



    public PaletteSectionViewModel(string title, string key, IReadOnlyList<ViewModelBase> cards)
    {
        Title = title;
        _key = key;
        _layout = GlobalContext.WindowLayoutModel;
        _cards = cards;
        _isCollapsed = _layout?.IsBlockGroupCollapsed(key) ?? false;
        _isSearching = false;

        if (_layout != null) _layout.BlockGroupCollapsedChanged += OnCollapsedChanged;
    }



    // ui events



    [RelayCommand]
    private void OnHeadingPressed()
    {
        if (_layout != null) _layout.SetBlockGroupCollapsed(_key, !IsCollapsed);
        else IsCollapsed = !IsCollapsed;
    }



    // model events



    private void OnCollapsedChanged(string key)
    {
        if (key == _key) IsCollapsed = _layout!.IsBlockGroupCollapsed(key);
    }



    // disposal



    public override void Dispose()
    {
        if (_layout != null) _layout.BlockGroupCollapsedChanged -= OnCollapsedChanged;
    }
}
