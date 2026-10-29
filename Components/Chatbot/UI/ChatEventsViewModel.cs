using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Chatbot.UI;



// the chatbot's Events tab: one bind box per stream event (Alerts), then the channel point redeems
// the collapsible test panel fires a binding with made-up values, so alerts can be tried without twitch
public partial class ChatEventsViewModel : ViewModelBase
{
    // private



    private readonly ChatbotComponentModel _model;
    private readonly ObservableCollection<RedeemRowViewModel> _redeems;
    private readonly IReadOnlyList<ChatEventSlotViewModel> _events;

    [ObservableProperty] private bool _isTestOpen;
    [ObservableProperty] private string _testUser;
    [ObservableProperty] private string _testAmount; // months, gift count, bits, viewers or cost
    [ObservableProperty] private string _testTier;
    [ObservableProperty] private string _testText; // message or redeem input



    // public



    public ObservableCollection<RedeemRowViewModel> Redeems => _redeems;
    public IReadOnlyList<ChatEventSlotViewModel> Events => _events;



    // constructor



    public ChatEventsViewModel(ChatbotComponentModel model)
    {
        _model = model;

        _isTestOpen = false;
        _testUser = "TestUser";
        _testAmount = "5";
        _testTier = "1";
        _testText = "";

        _redeems = [];
        foreach (var row in _model.Redeems.Rows)
            _redeems.Add(CreateRedeemRow(row));

        _events = _model.Events.Select(slot => new ChatEventSlotViewModel(slot, _model, TestEvent, _isTestOpen)).ToList();

        _model.Redeems.RowInserted += OnRedeemRowInserted;
        _model.Redeems.RowRemoved += OnRedeemRowRemoved;
    }



    // ui events



    [RelayCommand] private void OnTestTogglePressed() => IsTestOpen = !IsTestOpen;



    partial void OnIsTestOpenChanged(bool value)
    {
        foreach (var row in _redeems) row.ShowTest = value;
        foreach (var slot in _events) slot.ShowTest = value;
    }



    // test buttons run the binding through the model, like the real event would
    private void TestRedeem(RedeemRow row) =>
        row.Target.TryRun(ChatEvents.SampleParameters(null, TestUser, TestAmount, TestTier, TestText, row.Title));



    private void TestEvent(ChatEventSlot slot) =>
        slot.Target.TryRun(ChatEvents.SampleParameters(slot.Key, TestUser, TestAmount, TestTier, TestText));



    // model events



    private void OnRedeemRowInserted(RedeemRow row, int index) => _redeems.Insert(index, CreateRedeemRow(row));



    private void OnRedeemRowRemoved(RedeemRow row)
    {
        var viewModel = _redeems.FirstOrDefault(r => r.RepresentsModel(row));
        if (viewModel == null) return;

        _redeems.Remove(viewModel);
        viewModel.Dispose();
    }



    // helpers



    private RedeemRowViewModel CreateRedeemRow(RedeemRow row) => new(row, _model, TestRedeem, IsTestOpen);



    // disposal



    public override void Dispose()
    {
        _model.Redeems.RowInserted -= OnRedeemRowInserted;
        _model.Redeems.RowRemoved -= OnRedeemRowRemoved;

        foreach (var row in _redeems) row.Dispose();
        foreach (var slot in _events) slot.Dispose();
    }
}
