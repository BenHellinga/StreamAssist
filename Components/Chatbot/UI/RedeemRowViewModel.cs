using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Chatbot.UI;



// one redeem row: the reward title's text box, and its bind box (once there's a title or a binding)
public partial class RedeemRowViewModel : ViewModelBase
{
    // private



    private readonly RedeemRow _row;
    private readonly ChatTargetViewModel _target;
    private readonly Action<RedeemRow> _test;

    [ObservableProperty] private string _title;
    [ObservableProperty] private bool _showTarget;
    [ObservableProperty] private bool _showTest; // the test panel is open



    // public



    public ChatTargetViewModel Target => _target;



    // constructor



    public RedeemRowViewModel(RedeemRow row, ChatbotComponentModel model, Action<RedeemRow> test, bool showTest)
    {
        _row = row;
        _target = new(_row.Target, model);
        _test = test;

        _title = _row.Title;
        _showTarget = !_row.IsEmpty;
        _showTest = showTest;

        _row.TitleChanged += OnRowTitleChanged;
        _row.Target.ContentsChanged += OnRowTargetContentsChanged;
    }



    // public api



    public bool RepresentsModel(RedeemRow row) => row == _row;



    // ui events



    // typing in the box goes straight to the model
    partial void OnTitleChanged(string value) => _row.SetTitle(value ?? "");



    [RelayCommand] private void OnTestPressed() => _test(_row);



    // model events



    private void OnRowTitleChanged()
    {
        Title = _row.Title;
        ShowTarget = !_row.IsEmpty;
    }



    private void OnRowTargetContentsChanged() => ShowTarget = !_row.IsEmpty;



    // disposal



    public override void Dispose()
    {
        _row.TitleChanged -= OnRowTitleChanged;
        _row.Target.ContentsChanged -= OnRowTargetContentsChanged;
        _target.Dispose();
    }
}
