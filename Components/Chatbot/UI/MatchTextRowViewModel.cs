using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Chatbot.UI;



// one Match Text row: the pattern's text box, and the chat block slot to its right (once there's text or a block)
public partial class MatchTextRowViewModel : ViewModelBase
{
    // private



    private readonly MatchTextRow _row;
    private readonly BlockSlotViewModel _child;

    [ObservableProperty] private string _pattern;
    [ObservableProperty] private bool _showSlot;



    // public



    public BlockSlotViewModel Child => _child;



    // constructor



    public MatchTextRowViewModel(MatchTextRow row)
    {
        _row = row;
        _child = new(_row.Child);

        _pattern = _row.Pattern;
        _showSlot = !_row.IsEmpty;

        _row.PatternChanged += OnRowPatternChanged;
        _row.Child.BlockChanged += OnRowChildChanged;
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
        ShowSlot = !_row.IsEmpty;
    }



    private void OnRowChildChanged(BlockModel? _, BlockModel? __) => ShowSlot = !_row.IsEmpty;



    // disposal



    public override void Dispose()
    {
        _row.PatternChanged -= OnRowPatternChanged;
        _row.Child.BlockChanged -= OnRowChildChanged;
        _child.Dispose();
    }
}
