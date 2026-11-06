using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Chatbot.UI;



// the Events tab: one bind box per event, the app's (App) then twitch's (Alerts), then the channel point redeems with
// New redeem under them (creates a channel point reward on twitch). Bindings are tested with the editor's Test button.
// Dragging a redeem's binding onto another redeem row moves the whole row before that one
public partial class ChatEventsViewModel : ViewModelBase, IDroppable
{
    // private



    private readonly ChatbotComponentModel _model;
    private readonly RedeemList _redeemList; // the shown config's (only made while there's one), kept to unsubscribe from it
    private readonly ObservableCollection<RedeemRowViewModel> _redeems;
    private readonly IReadOnlyList<ChatTargetViewModel> _appEvents;
    private readonly IReadOnlyList<ChatTargetViewModel> _twitchEvents;

    [ObservableProperty] private bool _isNewRewardOpen;
    [ObservableProperty] private string _newRewardTitle;
    [ObservableProperty] private string _newRewardCost;
    [ObservableProperty] private string _newRewardPrompt;
    [ObservableProperty] private bool _newRewardNeedsInput;



    // public



    public ObservableCollection<RedeemRowViewModel> Redeems => _redeems;
    public IReadOnlyList<ChatTargetViewModel> AppEvents => _appEvents; // their bind boxes, in ChatEvents.All order
    public IReadOnlyList<ChatTargetViewModel> TwitchEvents => _twitchEvents;



    // constructor



    public ChatEventsViewModel(ChatbotComponentModel model)
    {
        _model = model;
        _redeemList = model.Redeems!;

        _isNewRewardOpen = false;
        _newRewardTitle = "";
        _newRewardCost = "100";
        _newRewardPrompt = "";
        _newRewardNeedsInput = false;

        _redeems = [];
        foreach (var row in _redeemList.Rows)
            _redeems.Add(CreateRedeemRow(row));

        _appEvents = _model.Events.Where(slot => ChatEvents.IsApp(slot.Key)).Select(slot => new ChatTargetViewModel(slot.Target, _model)).ToList();
        _twitchEvents = _model.Events.Where(slot => !ChatEvents.IsApp(slot.Key)).Select(slot => new ChatTargetViewModel(slot.Target, _model)).ToList();

        _redeemList.RowInserted += OnRedeemRowInserted;
        _redeemList.RowRemoved += OnRedeemRowRemoved;
        _redeemList.RowMoved += OnRedeemRowMoved;
    }



    // ui events



    // New redeem opens its panel under it, clicking it again closes it
    [RelayCommand] private void OnNewRewardTabPressed() => IsNewRewardOpen = !IsNewRewardOpen;



    // the model checks the values and the chatbot creates it on twitch (a redeem row appears once it exists), a problem
    // goes to the console
    [RelayCommand] private void OnCreateRewardPressed() =>
        _model.TryRequestCreateReward(NewRewardTitle, NewRewardCost, NewRewardPrompt, NewRewardNeedsInput);



    // drag drop (only the redeem rows' lines above them are targets here)



    // only reordering: the binding of one of the redeem rows
    public bool CanAcceptDropPayload(object payload) => FindRedeemRow(payload) != null;



    public bool PassesOn(object payload) => !CanAcceptDropPayload(payload);



    // zoneContext is the row it was dropped on, it goes before that one
    public void OnDroppedOn(object payload, object? zoneContext)
    {
        if (FindRedeemRow(payload) is not { } row) return;
        if (zoneContext is not RedeemRowViewModel before) return;

        _redeemList.TryMoveRow(row, _redeems.IndexOf(before));
    }



    private RedeemRow? FindRedeemRow(object payload) =>
        payload is BindableDragPayload dragged ? _redeemList.Rows.FirstOrDefault(r => r.Target.Bindable == dragged.Bindable) : null;



    // model events



    private void OnRedeemRowInserted(RedeemRow row, int index) => _redeems.Insert(index, CreateRedeemRow(row));



    private void OnRedeemRowMoved(RedeemRow _, int from, int to) => _redeems.Move(from, to);



    private void OnRedeemRowRemoved(RedeemRow row)
    {
        var viewModel = _redeems.FirstOrDefault(r => r.RepresentsModel(row));
        if (viewModel == null) return;

        _redeems.Remove(viewModel);
        viewModel.Dispose();
    }



    // helpers



    private RedeemRowViewModel CreateRedeemRow(RedeemRow row) => new(row, _model, this);



    // disposal



    public override void Dispose()
    {
        _redeemList.RowInserted -= OnRedeemRowInserted;
        _redeemList.RowRemoved -= OnRedeemRowRemoved;
        _redeemList.RowMoved -= OnRedeemRowMoved;

        foreach (var row in _redeems) row.Dispose();
        foreach (var target in _appEvents) target.Dispose();
        foreach (var target in _twitchEvents) target.Dispose();
    }
}
