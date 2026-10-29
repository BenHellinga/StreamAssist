using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Core.Mvvm;
using StreamAssist.Shell;

namespace StreamAssist.Components.Chatbot.UI;



// the chatbot's Chat tab: the chat command tree, with its own chat block list beside it
public partial class ChatCommandsViewModel : ViewModelBase
{
    // private



    private readonly WindowLayoutModel _layout;
    private readonly BlockSlotViewModel _root;
    private readonly BlockPaletteViewModel _chatPalette;

    [ObservableProperty] private GridLength _chatBlockListWidth; // bound two-way to the chat block list column



    // public



    public BlockSlotViewModel Root => _root;
    public BlockPaletteViewModel ChatPalette => _chatPalette;



    // constructor



    public ChatCommandsViewModel(ChatbotComponentModel model, WindowLayoutModel layout)
    {
        _layout = layout;
        _root = new(model.Root);
        _chatPalette = new(("Chat Blocks", BlockCategory.Chat));
        _chatBlockListWidth = new(_layout.GetPanelSize(WindowLayoutModel.ChatBlockListPanel, 200));

        _layout.PanelSizeChanged += OnLayoutPanelSizeChanged;
    }



    // ui events



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
        _chatPalette.Dispose();
    }
}
