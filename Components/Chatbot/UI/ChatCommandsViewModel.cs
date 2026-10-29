using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Core.Mvvm;
using StreamAssist.Shell;

namespace StreamAssist.Components.Chatbot.UI;



// the Chatbot tab: the chat command tree, with its own chat block list beside it
// the collapsible test panel sends a made-up chat message through the tree, like a real one
public partial class ChatCommandsViewModel : ViewModelBase
{
    // private



    private readonly ChatbotComponentModel _model;
    private readonly WindowLayoutModel _layout;
    private readonly BlockSlotViewModel _root;
    private readonly BlockSlotViewModel _editableRoot;
    private readonly BlockPaletteViewModel _chatPalette;

    [ObservableProperty] private GridLength _chatBlockListWidth; // bound two-way to the chat block list column

    [ObservableProperty] private bool _isTestOpen;
    [ObservableProperty] private string _testUser;
    [ObservableProperty] private string _testMessage;
    [ObservableProperty] private bool _testIsModerator;
    [ObservableProperty] private bool _testIsVip;
    [ObservableProperty] private bool _testIsSubscriber;
    [ObservableProperty] private string _testResult; // whether the last test message ran a command



    // public



    public BlockSlotViewModel Root => _root;
    public BlockSlotViewModel EditableRoot => _editableRoot;
    public BlockPaletteViewModel ChatPalette => _chatPalette;



    // constructor



    public ChatCommandsViewModel(ChatbotComponentModel model, WindowLayoutModel layout)
    {
        _model = model;
        _layout = layout;
        _root = new(model.Root);
        _editableRoot = new(model.EditableRoot);
        _chatPalette = new(("Chat Blocks", BlockCategory.Chat));
        _chatBlockListWidth = new(_layout.GetPanelSize(WindowLayoutModel.ChatBlockListPanel, 200));

        _isTestOpen = false;
        _testUser = "TestUser";
        _testMessage = "";
        _testIsModerator = false;
        _testIsVip = false;
        _testIsSubscriber = false;
        _testResult = "";

        _layout.PanelSizeChanged += OnLayoutPanelSizeChanged;
    }



    // ui events



    [RelayCommand] private void OnTestTogglePressed() => IsTestOpen = !IsTestOpen;



    // runs the message through the chat tree like a real one (the bot's own messages are the only ones skipped for real)
    [RelayCommand] private void OnSendTestPressed()
    {
        var parameters = ChatEvents.SampleChatParameters(TestUser, TestMessage, TestIsModerator, TestIsVip, TestIsSubscriber);
        var ran = _model.TryMatchMessage(new ChatMatchState(TestMessage, 0, parameters));
        TestResult = ran ? "Ran a command" : "No command matched";
    }



    // a changed message hasn't been tested yet
    partial void OnTestMessageChanged(string value) => TestResult = "";



    // the chat block list splitter was dragged
    partial void OnChatBlockListWidthChanged(GridLength value)
    {
        if (value.IsAbsolute) _layout.SetPanelSize(WindowLayoutModel.ChatBlockListPanel, value.Value);
    }



    // model events



    // something else (e.g. an action) changed the chat block list width
    private void OnLayoutPanelSizeChanged(string panel, double size)
    {
        if (panel == WindowLayoutModel.ChatBlockListPanel) ChatBlockListWidth = new(size);
    }



    // disposal



    public override void Dispose()
    {
        _layout.PanelSizeChanged -= OnLayoutPanelSizeChanged;

        _root.Dispose();
        _editableRoot.Dispose();
        _chatPalette.Dispose();
    }
}
