using System;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Core.Mvvm;
using StreamAssist.Shell;

namespace StreamAssist.Components.Chatbot.UI;



// the Chatbot and Events tabs: one page (the chat command tree, or alerts + redeems) and the shared editor in the bottom panel
// clicking a bind box opens its binding in the editor (an unsaved draft if it has none, like a key)
// the twitch logins are in the Connections tab
public partial class ChatbotComponentViewModel : ViewModelBase
{
    // private



    private readonly ChatbotComponentModel _model;
    private readonly BindableEditorViewModel _editor; // lives in the bottom panel, which disposes it
    private readonly ViewModelBase _page; // ChatCommandsViewModel or ChatEventsViewModel
    private ChatTarget? _selected;

    // the binding being edited, either the target's real one or a draft that isn't saved yet
    private BindableModel? _bindable;
    private bool _isDraft;




    // public



    public ViewModelBase Page => _page;



    // activators



    // the chat command tree (with its own chat block list)
    public static ChatbotComponentViewModel ActivateChatTab(ChatbotComponentModel model, WindowLayoutModel layout, PanelViewModel sidePanel, PanelViewModel bottomPanel) =>
        Activate(model, layout, sidePanel, bottomPanel, "No command selected", m => new ChatCommandsViewModel(m, layout));



    // alerts (sub, raid, ...) and channel point redeems
    public static ChatbotComponentViewModel ActivateEventsTab(ChatbotComponentModel model, WindowLayoutModel layout, PanelViewModel sidePanel, PanelViewModel bottomPanel) =>
        Activate(model, layout, sidePanel, bottomPanel, "No event selected", m => new ChatEventsViewModel(m));



    // the bottom panel edits the selected bind box's binding
    private static ChatbotComponentViewModel Activate(ChatbotComponentModel model, WindowLayoutModel layout, PanelViewModel sidePanel, PanelViewModel bottomPanel,
        string emptyText, Func<ChatbotComponentModel, ViewModelBase> createPage)
    {
        var editor = new BindableEditorViewModel(layout, emptyText);
        sidePanel.TransferContent(null);
        bottomPanel.TransferContent(editor);

        return new(model, editor, createPage);
    }



    // constructor



    public ChatbotComponentViewModel(ChatbotComponentModel model, BindableEditorViewModel editor, Func<ChatbotComponentModel, ViewModelBase> createPage)
    {
        _model = model;
        _editor = editor;
        _selected = null;
        _bindable = null;
        _isDraft = false;

        // a fresh tab starts with nothing open (before the tree is built, so no bind box starts selected)
        ChatTargetSelection.Set(null);

        // chat blocks have their own kinds of slot (rows of pattern + target, slot + bind box), tell the block editor how to show them
        // registered per tab rather than once, because bind boxes show their command and so need this model
        BlockViewModel.RegisterSlotViewModel<MatchTextRowList>(rows => new MatchTextRowListViewModel(rows, _model));
        BlockViewModel.RegisterSlotViewModel<ChatTarget>(target => new ChatTargetViewModel(target, _model));

        _page = createPage(_model);

        _model.Changed += OnModelChanged;
        ChatTargetSelection.Changed += OnSelectionChanged;
    }



    // ui events



    // a bind box anywhere in the tree was clicked (or the selection was cleared)
    private void OnSelectionChanged()
    {
        _selected?.BindableChanged -= OnSelectedBindableChanged;
        ClearEditor();

        _selected = ChatTargetSelection.Selected;
        if (_selected == null) return;

        _selected.BindableChanged += OnSelectedBindableChanged;
        ShowSelected();
    }



    // model events



    // something changed: close the editor if the bind box is gone (row removed, or a block went after it instead),
    // otherwise refresh its header (a pattern or title may have changed)
    private void OnModelChanged()
    {
        if (_selected == null) return;

        if (_selected.Child.Block == null && _model.Describe(_selected) != null)
        {
            _editor.Label = FormatLabel(_selected);
            return;
        }

        ChatTargetSelection.Set(null);
    }



    // the selected target was bound or unbound from somewhere else (e.g. a binding dropped on it, or its blocks all removed)
    private void OnSelectedBindableChanged()
    {
        // our own draft was just committed, keep editing it
        if (_selected == null || _selected.Bindable == _bindable) return;

        ShowSelected();
    }



    // the first change to a draft binds it to the target
    private void OnDraftChanged()
    {
        if (!_isDraft || _bindable == null || _selected == null) return;

        // e.g. the default On Pressed was deleted, there's still nothing worth saving
        if (!_bindable.HasBlock) return;

        _bindable.Changed -= OnDraftChanged;
        _isDraft = false;

        // couldn't bind (already logged), close the editor, which throws the draft away
        if (!_selected.TryBind(_bindable))
        {
            _isDraft = true;
            ChatTargetSelection.Set(null);
        }
    }



    // helpers



    // shows the selected target's binding, or a fresh unsaved draft if it has none
    private void ShowSelected()
    {
        if (_selected == null) return;
        ClearEditor();

        _isDraft = _selected.Bindable == null;
        _bindable = _selected.Bindable ?? BindableModel.CreateDefault();
        if (_isDraft) _bindable.Changed += OnDraftChanged;

        _editor.ShowBindable(_bindable, FormatLabel(_selected));
    }



    private void ClearEditor()
    {
        _editor.ClearBindable();

        // a draft that was never changed is thrown away, a real binding belongs to the target
        if (_bindable != null && _isDraft)
        {
            _bindable.Changed -= OnDraftChanged;
            _bindable.Dispose();
        }

        _bindable = null;
        _isDraft = false;
    }



    // the editor header, e.g. "Chat command: !pay {user} {amount}", "Redeem: Hydrate" or "Event: Raid"
    private string FormatLabel(ChatTarget target)
    {
        var description = _model.Describe(target);
        if (description == null) return "";

        var (kind, text) = description.Value;
        return text == "" && kind == "Chat command" ? "Chat command: (any message)" : $"{kind}: {text}";
    }



    // disposal



    public override void Dispose()
    {
        _model.Changed -= OnModelChanged;
        ChatTargetSelection.Changed -= OnSelectionChanged;

        _selected?.BindableChanged -= OnSelectedBindableChanged;
        _selected = null;
        ClearEditor();
        ChatTargetSelection.Set(null);

        _page.Dispose();
    }
}
