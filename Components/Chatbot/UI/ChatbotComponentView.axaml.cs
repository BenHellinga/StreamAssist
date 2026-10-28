using Avalonia;
using Avalonia.Input;
using Avalonia.VisualTree;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Components.Chatbot.Blocks;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Chatbot.UI;



public partial class ChatbotComponentView : ViewBase
{
    public ChatbotComponentView()
    {
        InitializeComponent();

        // buttons and drag handles mark their clicks as handled, so this only sees clicks on the card itself
        ChatTree.AddHandler(PointerPressedEvent, OnTreePointerPressed);
    }



    // clicking a Bindable chat block's card opens it in the bottom panel editor
    private void OnTreePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not ChatbotComponentViewModel vm) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        // the nearest block card around whatever was clicked
        for (var current = e.Source as Visual; current != null; current = current.GetVisualParent())
        {
            if (current is not StyledElement { DataContext: BlockViewModel block }) continue;

            if (block.Block is BindableChatBlock bindable) vm.SelectBindable(bindable);
            return;
        }
    }
}
