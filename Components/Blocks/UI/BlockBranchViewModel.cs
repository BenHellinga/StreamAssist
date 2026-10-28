using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Blocks.UI;



// one branch of an if: header ("if" / "else if"), its condition slot, and its then slot
public partial class BlockBranchViewModel : ViewModelBase
{
    // private



    private readonly BlockBranch _branch;
    private readonly BlockSlotViewModel _condition;
    private readonly BlockSlotViewModel _then;

    [ObservableProperty] private string _header;



    // public



    public BlockSlotViewModel Condition => _condition;
    public BlockSlotViewModel Then => _then;



    // constructor



    public BlockBranchViewModel(BlockBranch branch, string header)
    {
        _branch = branch;
        _condition = new(branch.Condition);
        _then = new(branch.Then);
        _header = header;
    }



    // public api



    public bool RepresentsModel(BlockBranch branch) => branch == _branch;



    // disposal



    public override void Dispose()
    {
        _condition.Dispose();
        _then.Dispose();
    }
}
