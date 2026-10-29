using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Chatbot.UI;



// one stream event's bind box (e.g. Raid), its text is the event name
public partial class ChatEventSlotViewModel : ViewModelBase
{
    // private



    private readonly ChatEventSlot _slot;
    private readonly ChatTargetViewModel _target;
    private readonly Action<ChatEventSlot> _test;

    [ObservableProperty] private bool _showTest; // the test panel is open



    // public



    public ChatTargetViewModel Target => _target;



    // constructor



    public ChatEventSlotViewModel(ChatEventSlot slot, ChatbotComponentModel model, Action<ChatEventSlot> test, bool showTest)
    {
        _slot = slot;
        _target = new(_slot.Target, model);
        _test = test;
        _showTest = showTest;
    }



    // ui events



    [RelayCommand] private void OnTestPressed() => _test(_slot);



    // disposal



    public override void Dispose() => _target.Dispose();
}
