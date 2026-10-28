using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Blocks.UI;



// one heading in the block list and the cards under it
public class BlockCategoryGroup(string title, IReadOnlyList<BlockCardViewModel> cards)
{
    public string Title { get; } = title;
    public IReadOnlyList<BlockCardViewModel> Cards { get; } = cards;
}



// the block list: every block type, grouped by category
public class BlockPaletteViewModel : ViewModelBase
{
    // private



    private readonly ObservableCollection<BlockCategoryGroup> _categories;



    // public



    public ObservableCollection<BlockCategoryGroup> Categories => _categories;



    // constructor



    public BlockPaletteViewModel()
    {
        _categories = [];

        var blocks = BlockResolver.DiscoverAll();

        AddCategory("Containers", BlockCategory.Container, blocks);
        AddCategory("Conditions", BlockCategory.Condition, blocks);
        AddCategory("Actions", BlockCategory.Action, blocks);
    }



    // helpers



    // empty categories are left out
    private void AddCategory(string title, BlockCategory category, IReadOnlyList<BlockModel> blocks)
    {
        var cards = blocks
            .Where(b => b.Category == category)
            .OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
            .Select(b => new BlockCardViewModel(b))
            .ToList();

        if (cards.Count > 0)
            _categories.Add(new(title, cards));
    }



    // disposal



    public override void Dispose()
    {
        foreach (var category in _categories)
            foreach (var card in category.Cards)
                card.Dispose();
    }
}
