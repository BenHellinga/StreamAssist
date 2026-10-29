using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Chatbot.UI;



// one Match Text row: the pattern's text box, and the bind box + chat block slot to its right (once there's text or something after it)
public partial class MatchTextRowViewModel : ViewModelBase
{
    // private



    private readonly MatchTextRow _row;
    private readonly ChatTargetViewModel _target;

    [ObservableProperty] private string _pattern;
    [ObservableProperty] private bool _showTarget;



    // public



    public ChatTargetViewModel Target => _target;



    // constructor



    public MatchTextRowViewModel(MatchTextRow row, ChatbotComponentModel model)
    {
        _row = row;
        _target = new(_row.Target, model);

        _pattern = _row.Pattern;
        _showTarget = !_row.IsEmpty;

        _row.PatternChanged += OnRowPatternChanged;
        _row.Target.ContentsChanged += OnRowTargetContentsChanged;
    }



    // public api



    public bool RepresentsModel(MatchTextRow row) => row == _row;



    // ui events



    // typing in the box goes straight to the model
    partial void OnPatternChanged(string value) => _row.SetPattern(value ?? "");



    // model events



    private void OnRowPatternChanged()
    {
        Pattern = _row.Pattern;
        ShowTarget = !_row.IsEmpty;
    }



    private void OnRowTargetContentsChanged() => ShowTarget = !_row.IsEmpty;



    // disposal



    public override void Dispose()
    {
        _row.PatternChanged -= OnRowPatternChanged;
        _row.Target.ContentsChanged -= OnRowTargetContentsChanged;
        _target.Dispose();
    }
}
