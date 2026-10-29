using System.Collections.Generic;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    [ObservableProperty] private BlockSlotViewModel? _rootSlot;
    [ObservableProperty] private bool _hasBindable;
    [ObservableProperty] private string _label;
    [ObservableProperty] private string _emptyText; // shown when nothing is being edited, e.g. "No key selected"
    [ObservableProperty] private IReadOnlyList<ParameterInfo> _parameters; // what Get Parameter can read here
    [ObservableProperty] private bool _hasParameters;
    [ObservableProperty] private bool _isParametersOpen; // the Parameters list is expanded (stays open between bindings)
    [ObservableProperty] private GridLength _blockListWidth; // bound two-way to the block list column



    // public



    public BlockPaletteViewModel Palette => _palette;



    // constructor



    public BindableEditorViewModel(WindowLayoutModel layout, string emptyText)
    {
        _layout = layout;
        _emptyText = emptyText;

        _palette = new(
            ("Containers", BlockCategory.Container),
            ("Actions", BlockCategory.Action),
            ("Any type", BlockCategory.Any),
            ("Bool", BlockCategory.Condition),
            ("Text", BlockCategory.Text),
            ("Int", BlockCategory.Int),
            ("Double", BlockCategory.Double),
            ("Char", BlockCategory.Char));

        _rootSlot = null;
        _hasBindable = false;
        _label = "";
        _parameters = [];
        _hasParameters = false;
        _isParametersOpen = false;
        _blockListWidth = new(_layout.GetPanelSize(WindowLayoutModel.BlockListPanel, 200));

        _layout.PanelSizeChanged += OnLayoutPanelSizeChanged;
    }



    // public api



    // starts editing a bindable, label is the header text (e.g. which key or command it belongs to),
    // parameters are what its trigger passes (none for keys)
    public void ShowBindable(BindableModel bindable, string label, IReadOnlyList<ParameterInfo>? parameters = null)
    {
        var oldSlot = RootSlot;
        RootSlot = new BlockSlotViewModel(bindable.Root);
        oldSlot?.Dispose();

        Label = label;
        SetParameters(parameters);
        HasBindable = true;
    }



    // e.g. a chat command's pattern changed, so what it captures did too
    public void SetParameters(IReadOnlyList<ParameterInfo>? parameters)
    {
        Parameters = parameters ?? [];
        HasParameters = Parameters.Count > 0;
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



    [RelayCommand] private void OnParametersTogglePressed() => IsParametersOpen = !IsParametersOpen;



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
