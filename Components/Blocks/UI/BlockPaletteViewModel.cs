using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Core.Mvvm;
using StreamAssist.Shell;

namespace StreamAssist.Components.Blocks.UI;



// one heading in the block list and the cards under it. Clicking the heading collapses / expands it, searching or not
// (a collapsed heading stays collapsed while searching, its heading shows how many blocks match)
public partial class BlockGroupViewModel : ViewModelBase
{
    // private



    private readonly IReadOnlyList<BlockCardViewModel> _allCards; // never filtered
    private readonly Action<bool> _setCollapsed;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Heading), nameof(HeadingText))] private IReadOnlyList<BlockCardViewModel> _cards; // the ones matching the search
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowCards), nameof(Heading), nameof(Arrow))] private bool _isCollapsed;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ShowCards), nameof(Heading), nameof(HeadingText))] private bool _isSearching;



    // public



    public string Title { get; }
    public BlockGroup Group { get; }
    public IReadOnlyList<BlockCardViewModel> AllCards => _allCards;
    public bool ShowCards => !IsCollapsed;
    public string HeadingText => $"{Title} ({Cards.Count})"; // with how many it shows (all, or the ones matching)
    public string Arrow => ShowCards ? "▾" : "▸";
    public string Heading => $"{HeadingText} {Arrow}";



    // constructor



    public BlockGroupViewModel(string title, BlockGroup group, IReadOnlyList<BlockCardViewModel> cards, bool isCollapsed, Action<bool> setCollapsed)
    {
        Title = title;
        Group = group;
        _allCards = cards;
        _setCollapsed = setCollapsed;

        _cards = cards;
        _isCollapsed = isCollapsed;
        _isSearching = false;
    }



    // ui events



    // asks for the change, the heading follows once it's made (the palette listens to where it's kept)
    [RelayCommand] private void OnHeadingPressed() => _setCollapsed(!IsCollapsed);



    // disposal



    public override void Dispose()
    {
        foreach (var card in _allCards)
            card.Dispose();
    }
}



// a block list: the block types of the given headings, filtered by the type dropdown (the kind of slot they'd go in,
// only kinds this list has; clicking an empty slot picks its kind) and the search box (their names). Which headings
// are collapsed is kept in the window layout (Config/window.json), so it stays the same in every block list and after a
// restart
public partial class BlockPaletteViewModel : ViewModelBase, IListSearch
{
    // private



    // wait this long after the last keystroke before filtering
    private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(500);

    private readonly WindowLayoutModel? _layout; // null: collapsing only lasts while this list is open
    private readonly List<BlockGroupViewModel> _allGroups; // every heading, never filtered
    private readonly ObservableCollection<BlockGroupViewModel> _groups; // the headings with something to show
    private IDisposable? _pendingSearch;

    [ObservableProperty] private string _searchText;
    [ObservableProperty] private string _selectedTag;
    [ObservableProperty] private bool _hasResults;



    // public



    public ObservableCollection<BlockGroupViewModel> Groups => _groups;
    public IReadOnlyList<string> Tags { get; } // All, then the kinds of slot this list's blocks go in



    // constructor



    // headings appear in the order given, e.g. new(layout, BlockGroup.Control, BlockGroup.Logic)
    public BlockPaletteViewModel(WindowLayoutModel? layout, params BlockGroup[] groups)
    {
        _layout = layout;
        _allGroups = [];
        _groups = [];
        _pendingSearch = null;

        _searchText = "";
        _selectedTag = SlotTags.All;
        _hasResults = true;

        var blocks = BlockResolver.DiscoverAll();
        foreach (var group in groups)
            AddGroup(group, blocks);
        Tags = SlotTags.TagsFor(_allGroups.SelectMany(g => g.AllCards).Select(c => c.Block));

        SubscribeToLayout();
        ApplySearch();
    }



    // public api



    // the heading's title, e.g. "Twitch"
    public static string TitleOf(BlockGroup group) => group switch
    {
        BlockGroup.Obs => "OBS",
        BlockGroup.Chat => "Chat Blocks",
        _ => group.ToString(),
    };



    // ui events



    // restart the wait on every keystroke, so filtering only happens once typing pauses
    partial void OnSearchTextChanged(string value)
    {
        _pendingSearch?.Dispose();
        _pendingSearch = DispatcherTimer.RunOnce(() =>
        {
            _pendingSearch = null;
            ApplySearch();
        }, SearchDelay);
    }



    partial void OnSelectedTagChanged(string value) => ApplySearch();



    // model events



    // an empty slot was clicked: show what fits it, if this list has any
    private void OnSlotTagRequested(string tag)
    {
        if (Tags.Contains(tag)) SelectedTag = tag;
    }



    private void OnBlockGroupCollapsedChanged(string name)
    {
        foreach (var group in _allGroups.Where(g => g.Group.ToString() == name))
            group.IsCollapsed = _layout!.IsBlockGroupCollapsed(name);
    }



    // helpers



    private void AddGroup(BlockGroup group, IReadOnlyList<BlockModel> blocks)
    {
        var cards = blocks
            .Where(b => b.Group == group)
            .OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
            .Select(b => new BlockCardViewModel(b))
            .ToList();

        var name = group.ToString();
        BlockGroupViewModel? created = null;
        created = new(TitleOf(group), group, cards, _layout?.IsBlockGroupCollapsed(name) ?? false, collapsed =>
        {
            if (_layout != null) _layout.SetBlockGroupCollapsed(name, collapsed);
            else created!.IsCollapsed = collapsed;
        });

        _allGroups.Add(created);
    }



    // shows the cards of the picked kind whose name contains the search text (any case), headings with nothing left are
    // hidden
    private void ApplySearch()
    {
        var search = SearchText.Trim();
        var tag = SelectedTag ?? SlotTags.All;
        _groups.Clear();

        foreach (var group in _allGroups)
        {
            group.Cards = group.AllCards.Where(c => SlotTags.Fits(c.Block, tag) && c.Name.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
            group.IsSearching = search != "" || tag != SlotTags.All;

            if (group.Cards.Count > 0)
                _groups.Add(group);
        }

        HasResults = _groups.Count > 0;
    }



    // subscriptions



    private void SubscribeToLayout()
    {
        if (_layout != null) _layout.BlockGroupCollapsedChanged += OnBlockGroupCollapsedChanged;
        SlotTags.Requested += OnSlotTagRequested;
    }



    private void UnsubscribeFromLayout()
    {
        if (_layout != null) _layout.BlockGroupCollapsedChanged -= OnBlockGroupCollapsedChanged;
        SlotTags.Requested -= OnSlotTagRequested;
    }



    // disposal



    public override void Dispose()
    {
        UnsubscribeFromLayout();
        _pendingSearch?.Dispose();

        foreach (var group in _allGroups)
            group.Dispose();
    }
}
