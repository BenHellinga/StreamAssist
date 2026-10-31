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
    private readonly Func<ChatbotComponentModel, ViewModelBase> _createPage;
    private readonly IConfigSet _configs; // the Chat tab's or the Events tab's
    private readonly ConfigBarViewModel _configBar;
    private ChatTarget? _selected;
    private object? _shownConfig; // the config the page was made from (null: none, the page says to make one)

    [ObservableProperty] private ViewModelBase _page; // ChatCommandsViewModel or ChatEventsViewModel, made again when the config switches



    // public



    public ConfigBarViewModel ConfigBar => _configBar;



    // activators



    // the chat command tree (with its own chat block list)
    public static ChatbotComponentViewModel ActivateChatTab(ChatbotComponentModel model, WindowLayoutModel layout, PanelViewModel sidePanel, PanelViewModel bottomPanel) =>
        Activate(model, layout, sidePanel, bottomPanel, "No command selected", model.ChatConfigs, m => new ChatCommandsViewModel(m, layout));



    // alerts (sub, raid, ...) and channel point redeems
    public static ChatbotComponentViewModel ActivateEventsTab(ChatbotComponentModel model, WindowLayoutModel layout, PanelViewModel sidePanel, PanelViewModel bottomPanel) =>
        Activate(model, layout, sidePanel, bottomPanel, "No event selected", model.EventsConfigs, m => new ChatEventsViewModel(m));



    // the bottom panel edits the selected bind box's binding
    private static ChatbotComponentViewModel Activate(ChatbotComponentModel model, WindowLayoutModel layout, PanelViewModel sidePanel, PanelViewModel bottomPanel,
        string emptyText, IConfigSet configs, Func<ChatbotComponentModel, ViewModelBase> createPage)
    {
        var editor = new BindableEditorViewModel(layout, emptyText);
        sidePanel.TransferContent(null);
        bottomPanel.TransferContent(editor);

        return new(model, editor, configs, createPage);
    }



    // constructor



    public ChatbotComponentViewModel(ChatbotComponentModel model, BindableEditorViewModel editor, IConfigSet configs,
        Func<ChatbotComponentModel, ViewModelBase> createPage)
    {
        _model = model;
        _editor = editor;
        _configs = configs;
        _createPage = createPage;
        _configBar = new(configs);
        _selected = null;
        _shownConfig = configs.SelectedItem;

        // a fresh tab starts with nothing open (before the tree is built, so no bind box starts selected)
        ChatTargetSelection.Set(null);

        // chat blocks have their own kinds of slot (rows of pattern + target, slot + bind box), tell the block editor how to show them
        // registered per tab rather than once, because bind boxes show their command and so need this model
        BlockViewModel.RegisterSlotViewModel<MatchTextRowList>(rows => new MatchTextRowListViewModel(rows, _model));
        BlockViewModel.RegisterSlotViewModel<ChatTarget>(target => new ChatTargetViewModel(target, _model));

        _page = CreatePage();

        _model.Changed += OnModelChanged;
        _configs.ListChanged += OnConfigsChanged;
        ChatTargetSelection.Changed += OnSelectionChanged;
    }



    // ui events



    // a bind box anywhere in the tree was clicked (or the selection was cleared)
    private void OnSelectionChanged()
    {
        _selected?.BindableChanged -= OnSelectedBindableChanged;
        _editor.ClearBindable();

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
            _editor.SetParameters(_model.FindParameters(_selected));
            return;
        }

        ChatTargetSelection.Set(null);
    }



    // another config was picked (or one added or deleted): if the page no longer shows the selected config's trees, it's
    // made again from them, with nothing open in the editor
    private void OnConfigsChanged()
    {
        if (_configs.SelectedItem == _shownConfig) return;

        ChatTargetSelection.Set(null);
        var old = Page;
        _shownConfig = _configs.SelectedItem;
        Page = CreatePage();
        old.Dispose();
    }



    // the selected target was bound or unbound from somewhere else (e.g. a binding dropped on it, or its blocks all removed)
    private void OnSelectedBindableChanged()
    {
        // our own draft was just saved, keep editing it
        if (_selected == null || _selected.Bindable == _editor.Bindable) return;

        ShowSelected();
    }



    // helpers



    // the config's page, or a note that there's nothing to bind until a config is made
    private ViewModelBase CreatePage() =>
        _configs.SelectedItem == null ? new NoConfigViewModel() : _createPage(_model);



    // shows the selected target's binding, or a fresh empty draft (bound to it on its first change) if it has none
    private void ShowSelected()
    {
        if (_selected == null) return;

        var target = _selected;
        var label = FormatLabel(target);
        var parameters = _model.FindParameters(target);

        if (target.Bindable != null) _editor.ShowBindable(target.Bindable, label, parameters);
        else _editor.ShowDraft(label, target.TryBind, parameters, startWithOnPressed: false); // chat triggers just fire once
    }



    // the editor header, e.g. "Chat command: !pay {user} {amount}", "Redeem: Hydrate" or "Alert: Raid"
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
        _configs.ListChanged -= OnConfigsChanged;
        ChatTargetSelection.Changed -= OnSelectionChanged;

        _selected?.BindableChanged -= OnSelectedBindableChanged;
        _selected = null;
        _editor.ClearBindable();
        ChatTargetSelection.Set(null);

        Page.Dispose();
        _configBar.Dispose();
    }
}
