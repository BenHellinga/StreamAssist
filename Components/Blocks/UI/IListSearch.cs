using System.Collections.Generic;

namespace StreamAssist.Components.Blocks.UI;



// a card list's search row (ListSearchView): the name search, and the type dropdown with only the kinds the list has
public interface IListSearch
{
    string SearchText { get; set; }
    IReadOnlyList<string> Tags { get; }
    string SelectedTag { get; set; }
}
