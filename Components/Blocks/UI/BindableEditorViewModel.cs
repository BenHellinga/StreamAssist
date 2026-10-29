using System;
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
// something with no binding yet (an unbound key or bind box) gets a draft: an unsaved binding that's only saved
// (by the owner's save callback) on its first change, and thrown away if nothing was changed
public partial class BindableEditorViewModel : ViewModelBase
{
    // private



    private readonly WindowLayoutModel _layout;
    private readonly BlockPaletteViewModel _palette;
    private BindableModel? _bindable; // what's being edited, a saved binding or a draft
    private Func<BindableModel, bool>? _saveDraft; // set while it's a draft

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
    public BindableModel? Bindable => _bindable;



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

        _bindable = null;
        _saveDraft = null;
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



    // starts editing a saved binding, label is the header text (e.g. which key or command it belongs to),
    // parameters are what its trigger passes (none for keys)
    public void ShowBindable(BindableModel bindable, string label, IReadOnlyList<ParameterInfo>? parameters = null) =>
        Show(bindable, label, parameters, saveDraft: null);



    // starts editing a fresh unsaved binding for something with no binding yet, its first change that leaves it
    // with a block calls saveDraft (e.g. bind it to the key), which returns false if it couldn't (it stays a draft)
    // keys start with an On Pressed (they have a press and a release), things that just fire once start empty
    public void ShowDraft(string label, Func<BindableModel, bool> saveDraft, IReadOnlyList<ParameterInfo>? parameters = null, bool startWithOnPressed = true) =>
        Show(startWithOnPressed ? BindableModel.CreateDefault() : new BindableModel(), label, parameters, saveDraft);



    // e.g. a chat command's pattern changed, so what it captures did too
    public void SetParameters(IReadOnlyList<ParameterInfo>? parameters)
    {
        Parameters = parameters ?? [];
        HasParameters = Parameters.Count > 0;
    }



    // stops editing, a draft that was never changed is thrown away (a saved binding isn't touched)
    public void ClearBindable()
    {
        var oldSlot = RootSlot;
        RootSlot = null;
        oldSlot?.Dispose();

        if (_bindable != null && _saveDraft != null)
        {
            _bindable.Changed -= OnDraftChanged;
            _bindable.Dispose();
        }

        _bindable = null;
        _saveDraft = null;
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



    // the first change to a draft saves it
    private void OnDraftChanged()
    {
        // e.g. the default On Pressed was deleted, there's still nothing worth saving
        if (_bindable == null || _saveDraft == null || !_bindable.HasBlock) return;

        var save = _saveDraft;
        _bindable.Changed -= OnDraftChanged;
        _saveDraft = null;

        // couldn't save it (already logged), it stays a draft
        if (!save(_bindable))
        {
            _saveDraft = save;
            _bindable.Changed += OnDraftChanged;
        }
    }



    // something else (e.g. an action) changed the block list width
    private void OnLayoutPanelSizeChanged(string panel, double size)
    {
        if (panel == WindowLayoutModel.BlockListPanel) BlockListWidth = new(size);
    }



    // helpers



    private void Show(BindableModel bindable, string label, IReadOnlyList<ParameterInfo>? parameters, Func<BindableModel, bool>? saveDraft)
    {
        ClearBindable();

        _bindable = bindable;
        _saveDraft = saveDraft;
        if (_saveDraft != null) _bindable.Changed += OnDraftChanged;

        RootSlot = new BlockSlotViewModel(bindable.Root);
        Label = label;
        SetParameters(parameters);
        HasBindable = true;
    }



    // disposal



    public override void Dispose()
    {
        ClearBindable();

        _layout.PanelSizeChanged -= OnLayoutPanelSizeChanged;
        _palette.Dispose();
    }
}
