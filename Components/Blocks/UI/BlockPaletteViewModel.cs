using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Blocks.UI;



// one heading in the block list and the cards under it
public class BlockCategoryGroup(string title, IReadOnlyList<BlockCardViewModel> cards)
{
    public string Title { get; } = title;
    public IReadOnlyList<BlockCardViewModel> Cards { get; } = cards;
}



// a block list: the block types of the given categories, one heading each, filtered by the search box
public partial class BlockPaletteViewModel : ViewModelBase
{
    // private



    // wait this long after the last keystroke before filtering
    private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(500);

    private readonly List<BlockCategoryGroup> _allCategories; // every card, never filtered
    private readonly ObservableCollection<BlockCategoryGroup> _categories; // what's shown
    private IDisposable? _pendingSearch;

    [ObservableProperty] private string _searchText;
    [ObservableProperty] private bool _hasResults;



    // public



    public ObservableCollection<BlockCategoryGroup> Categories => _categories;



    // constructor



    // e.g. new(("Actions", BlockCategory.Action)), headings appear in the order given
    public BlockPaletteViewModel(params (string Title, BlockCategory Category)[] categories)
    {
        _allCategories = [];
        _categories = [];
        _pendingSearch = null;

        _searchText = "";
        _hasResults = true;

        var blocks = BlockResolver.DiscoverAll();

        foreach (var (title, category) in categories)
            AddCategory(title, category, blocks);

        ApplySearch();
    }



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



    // helpers



    private void AddCategory(string title, BlockCategory category, IReadOnlyList<BlockModel> blocks)
    {
        var cards = blocks
            .Where(b => b.Category == category)
            .OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
            .Select(b => new BlockCardViewModel(b))
            .ToList();

        _allCategories.Add(new(title, cards));
    }



    // shows the cards whose name contains the search text (any case), categories with nothing left are hidden
    private void ApplySearch()
    {
        var search = SearchText.Trim();
        _categories.Clear();

        foreach (var category in _allCategories)
        {
            var cards = category.Cards
                .Where(c => c.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (cards.Count > 0)
                _categories.Add(new(category.Title, cards));
        }

        HasResults = _categories.Count > 0;
    }



    // disposal



    public override void Dispose()
    {
        _pendingSearch?.Dispose();

        foreach (var category in _allCategories)
            foreach (var card in category.Cards)
                card.Dispose();
    }
}
