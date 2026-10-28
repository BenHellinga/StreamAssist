using System.Collections.Generic;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Components.Chatbot.Background;
using StreamAssist.Components.Chatbot.Blocks;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Core.Mvvm;
using StreamAssist.Shell;

namespace StreamAssist.Components.Chatbot.UI;



// the chatbot tab: twitch app credentials, the two account logins, the chat block tree and the chat block list
// clicking a Bindable chat block opens its bindable in the shared editor in the bottom panel
public partial class ChatbotComponentViewModel : ViewModelBase
{
    // private



    private readonly ChatbotComponentModel _model;
    private readonly ChatbotManager _manager;
    private readonly WindowLayoutModel _layout;
    private readonly BindableEditorViewModel _editor; // lives in the bottom panel, which disposes it
    private readonly BlockSlotViewModel _root;
    private readonly BlockPaletteViewModel _chatPalette;
    private BindableChatBlock? _selected;

    [ObservableProperty] private string _clientIdInput;
    [ObservableProperty] private string _clientSecretInput;
    [ObservableProperty] private bool _isStreamerLoggedIn; // connected or connecting
    [ObservableProperty] private bool _isBotLoggedIn;
    [ObservableProperty] private string _streamerStatus;
    [ObservableProperty] private string _botStatus;
    [ObservableProperty] private GridLength _chatBlockListWidth; // bound two-way to the chat block list column



    // public



    public BlockSlotViewModel Root => _root;
    public BlockPaletteViewModel ChatPalette => _chatPalette;



    // static constructor



    // Match Text blocks have their own kind of slot (rows of pattern + block), tell the block editor how to show it
    static ChatbotComponentViewModel()
    {
        BlockViewModel.RegisterSlotViewModel<MatchTextRowList>(rows => new MatchTextRowListViewModel(rows));
    }



    // activator



    public static ChatbotComponentViewModel ActivateTab(ChatbotComponentModel model, ChatbotManager manager, WindowLayoutModel layout, PanelViewModel sidePanel, PanelViewModel bottomPanel)
    {
        // the chat block list is part of this tab, the bottom panel edits the selected Bindable's blocks
        var editor = new BindableEditorViewModel(layout, "No bindable selected, click a Bindable chat block to edit it");
        sidePanel.TransferContent(null);
        bottomPanel.TransferContent(editor);

        return new(model, manager, layout, editor);
    }



    // constructor



    public ChatbotComponentViewModel(ChatbotComponentModel model, ChatbotManager manager, WindowLayoutModel layout, BindableEditorViewModel editor)
    {
        _model = model;
        _manager = manager;
        _layout = layout;
        _editor = editor;
        _root = new(_model.Root);
        _chatPalette = new(("Chat Blocks", BlockCategory.Chat));
        _selected = null;

        _clientIdInput = "";
        _clientSecretInput = "";
        _streamerStatus = "";
        _botStatus = "";
        _chatBlockListWidth = new(_layout.GetPanelSize(WindowLayoutModel.ChatBlockListPanel, 200));

        OnConnectionChanged();
        UpdateStatus();

        _model.ConnectionChanged += OnConnectionChanged;
        _model.Changed += OnModelChanged;
        _manager.StatusChanged += OnStatusChanged;
        _layout.PanelSizeChanged += OnLayoutPanelSizeChanged;
    }



    // public api



    // opens a Bindable chat block's bindable in the bottom panel editor
    public void SelectBindable(BindableChatBlock block)
    {
        if (block == _selected) return;

        _selected = block;
        _editor.ShowBindable(block.Bindable, DescribePath(block));
    }



    // ui events



    // streamer: logging in = connecting (it asks for a code the first time)
    [RelayCommand] private void OnStreamerLogInPressed()
    {
        _model.SetCredentials(ClientIdInput, ClientSecretInput);
        _model.SetIsEnabled(true);
    }



    // streamer: logging out disconnects and forgets the login
    [RelayCommand] private void OnStreamerLogOutPressed()
    {
        _model.SetIsEnabled(false);
        _manager.LogOut(TwitchAccount.Streamer);
    }



    [RelayCommand] private void OnBotLogInPressed()
    {
        _model.SetCredentials(ClientIdInput, ClientSecretInput);
        _manager.StartBotLogin();
    }



    [RelayCommand] private void OnBotLogOutPressed() => _manager.LogOut(TwitchAccount.Bot);



    // the chat block list splitter was dragged
    partial void OnChatBlockListWidthChanged(GridLength value)
    {
        if (value.IsAbsolute) _layout.SetPanelSize(WindowLayoutModel.ChatBlockListPanel, value.Value);
    }



    // model events



    private void OnConnectionChanged()
    {
        ClientIdInput = _model.ClientId;
        ClientSecretInput = _model.ClientSecret;
        IsStreamerLoggedIn = _model.IsEnabled;
    }



    // the tree changed: drop the selection if its block was removed, otherwise refresh its path (a pattern may have changed)
    private void OnModelChanged()
    {
        if (_selected == null) return;

        var path = new List<string>();
        if (FindPath(_model.Root.Block, _selected, path))
        {
            _editor.Label = FormatPath(path);
            return;
        }

        _selected = null;
        _editor.ClearBindable();
    }



    // something else (e.g. an action) changed the chat block list width
    private void OnLayoutPanelSizeChanged(string panel, double size)
    {
        if (panel == WindowLayoutModel.ChatBlockListPanel) ChatBlockListWidth = new(size);
    }



    // background events



    private void OnStatusChanged() => UpdateStatus();



    // helpers



    private void UpdateStatus()
    {
        StreamerStatus = _manager.StatusMessage;
        BotStatus = _manager.BotStatusMessage;
        IsBotLoggedIn = _manager.BotLogin != null;
    }



    // the editor header, e.g. "Chat command: ! › lurk\b"
    private string DescribePath(BindableChatBlock block)
    {
        var path = new List<string>();
        FindPath(_model.Root.Block, block, path);
        return FormatPath(path);
    }



    private static string FormatPath(List<string> path) =>
        path.Count == 0 ? "Chat command: (any message)" : $"Chat command: {string.Join(" › ", path)}";



    // walks the tree collecting each Match Text row pattern / Parameter name on the way down to the block
    private static bool FindPath(BlockSlotList list, BlockModel target, List<string> path)
    {
        foreach (var block in list.Blocks)
            if (FindPath(block, target, path)) return true;

        return false;
    }



    private static bool FindPath(BlockModel? block, BlockModel target, List<string> path)
    {
        if (block == null) return false;
        if (block == target) return true;

        switch (block)
        {
            case MatchTextChatBlock match:
                foreach (var row in match.Rows.Rows)
                {
                    path.Add(row.Pattern);
                    if (FindPath(row.Child.Block, target, path)) return true;
                    path.RemoveAt(path.Count - 1);
                }
                break;

            case ParameterChatBlock parameter:
                path.Add($"<{parameter.ParameterName}>");
                if (FindPath(parameter.Children, target, path)) return true;
                path.RemoveAt(path.Count - 1);
                break;
        }

        return false;
    }



    // disposal



    public override void Dispose()
    {
        _model.ConnectionChanged -= OnConnectionChanged;
        _model.Changed -= OnModelChanged;
        _manager.StatusChanged -= OnStatusChanged;
        _layout.PanelSizeChanged -= OnLayoutPanelSizeChanged;

        _root.Dispose();
        _chatPalette.Dispose();
    }
}
