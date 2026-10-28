using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Core.Mvvm;
using StreamAssist.Shell;

namespace StreamAssist.Components.Blocks.UI;



// the bottom panel block editor: one bindable's block tree on the left, the block list on the right
// every tab that edits bindables uses this (the keyboard's KeybindViewModel builds on it, the chatbot uses it directly)
public partial class BindableEditorViewModel : ViewModelBase
{
    // private



    private readonly WindowLayoutModel _layout;
    private readonly BlockPaletteViewModel _palette;
    private readonly string _emptyText;

    [ObservableProperty] private BlockSlotViewModel? _rootSlot;
    [ObservableProperty] private bool _hasBindable;
    [ObservableProperty] private string _label;
    [ObservableProperty] private GridLength _blockListWidth; // bound two-way to the block list column



    // public



    public BlockPaletteViewModel Palette => _palette;
    public string EmptyText => _emptyText; // shown when nothing is being edited, e.g. "No key selected"



    // constructor



    public BindableEditorViewModel(WindowLayoutModel layout, string emptyText)
    {
        _layout = layout;
        _emptyText = emptyText;

        _palette = new(
            ("Containers", BlockCategory.Container),
            ("Conditions", BlockCategory.Condition),
            ("Actions", BlockCategory.Action));

        _rootSlot = null;
        _hasBindable = false;
        _label = "";
        _blockListWidth = new(_layout.GetPanelSize(WindowLayoutModel.BlockListPanel, 200));

        _layout.PanelSizeChanged += OnLayoutPanelSizeChanged;
    }



    // public api



    // starts editing a bindable, label is the header text (e.g. which key or command it belongs to)
    public void ShowBindable(BindableModel bindable, string label)
    {
        var oldSlot = RootSlot;
        RootSlot = new BlockSlotViewModel(bindable.Root);
        oldSlot?.Dispose();

        Label = label;
        HasBindable = true;
    }



    // stops editing (the bindable itself isn't touched)
    public void ClearBindable()
    {
        var oldSlot = RootSlot;
        RootSlot = null;
        oldSlot?.Dispose();

        HasBindable = false;
    }



    // ui events



    // the block list splitter was dragged
    partial void OnBlockListWidthChanged(GridLength value)
    {
        if (value.IsAbsolute) _layout.SetPanelSize(WindowLayoutModel.BlockListPanel, value.Value);
    }



    // model events



    // something else (e.g. an action) changed the block list width
    private void OnLayoutPanelSizeChanged(string panel, double size)
    {
        if (panel == WindowLayoutModel.BlockListPanel) BlockListWidth = new(size);
    }



    // disposal



    public override void Dispose()
    {
        ClearBindable();

        _layout.PanelSizeChanged -= OnLayoutPanelSizeChanged;
        _palette.Dispose();
    }
}
