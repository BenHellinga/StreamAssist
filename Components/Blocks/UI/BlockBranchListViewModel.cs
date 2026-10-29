using System.Collections.ObjectModel;
using System.Linq;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Blocks.UI;



// view model for an if's branches: dropping a condition on a branch starts a new branch before it (the zone at the end
// adds one last)
public class BlockBranchListViewModel : ViewModelBase, IDroppable
{
    // private



    private readonly BlockBranchList _list;
    private readonly ObservableCollection<BlockBranchViewModel> _branches;



    // public



    public ObservableCollection<BlockBranchViewModel> Branches => _branches;



    // constructor



    public BlockBranchListViewModel(BlockBranchList list)
    {
        _list = list;
        _branches = [];

        foreach (var branch in _list.Branches)
            _branches.Add(new(branch, ""));

        UpdateHeaders();
        SubscribeToBlockBranchList(_list);
    }



    // drag drop



    public bool CanAcceptDropPayload(object payload) =>
        payload is BlockDragPayload blockPayload
        && _list.Accepts(blockPayload.Block)
        && !blockPayload.Block.ContainsSlot(_list);



    // zoneContext is the branch it was dropped on, or null for the zone at the end
    public void OnDroppedOn(object payload, object? zoneContext)
    {
        if (payload is not BlockDragPayload blockPayload) return;

        var index = zoneContext is BlockBranchViewModel beforeBranch ? _branches.IndexOf(beforeBranch) : _branches.Count;
        if (index < 0) index = _branches.Count;

        var copy = BlockResolver.DeepCopy(blockPayload.Block);
        if (copy == null) return;

        if (!_list.TryInsertBranch(copy, index))
        {
            copy.Dispose();
            return;
        }

        blockPayload.RemoveFromSource?.Invoke();
    }



    // model events



    private void OnBranchInserted(BlockBranch branch, int index)
    {
        _branches.Insert(index, new(branch, ""));
        UpdateHeaders();
    }



    private void OnBranchRemoved(BlockBranch branch)
    {
        var viewModel = _branches.FirstOrDefault(b => b.RepresentsModel(branch));
        if (viewModel == null) return;

        _branches.Remove(viewModel);
        viewModel.Dispose();
        UpdateHeaders();
    }



    // helpers



    // the first branch reads "if", the rest "else if"
    private void UpdateHeaders()
    {
        for (int i = 0; i < _branches.Count; i++)
            _branches[i].Header = i == 0 ? "if" : "else if";
    }



    private void SubscribeToBlockBranchList(BlockBranchList list)
    {
        list.BranchInserted += OnBranchInserted;
        list.BranchRemoved += OnBranchRemoved;
    }



    private void UnsubscribeFromBlockBranchList(BlockBranchList list)
    {
        list.BranchInserted -= OnBranchInserted;
        list.BranchRemoved -= OnBranchRemoved;
    }



    // disposal



    public override void Dispose()
    {
        UnsubscribeFromBlockBranchList(_list);

        foreach (var branch in _branches)
            branch.Dispose();
    }
}
