using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Blocks.Model;



// an ordered list of condition/then branches (the if / else if part of an If block)
// dropping a condition into a gap creates a branch, and a branch disappears once both its slots are empty
public sealed class BlockBranchList : BlockSlotBase
{
    // private



    private readonly List<BlockBranch> _branches;



    // public



    public IReadOnlyList<BlockBranch> Branches => _branches;
    public override IReadOnlyList<BlockModel> Blocks => [];
    public override IReadOnlyList<BlockSlotBase> InnerSlots => _branches.SelectMany(b => new BlockSlotBase[] { b.Condition, b.Then }).ToList();



    // events



    public event Action<BlockBranch, int>? BranchInserted;
    public event Action<BlockBranch>? BranchRemoved;



    // constructor



    // the list itself only accepts conditions (dropping one in a gap makes a new branch)
    public BlockBranchList(string label) : base(label, typeof(ConditionBlock))
    {
        _branches = [];
    }



    // branch management



    public bool TryInsertBranch(BlockModel condition, int index)
    {
        if (!Accepts(condition))
        {
            AppLog.WriteError($"can't place {condition.Name}, only conditions start a branch");
            return false;
        }

        if (index < 0 || index > _branches.Count)
        {
            AppLog.WriteError("can't place condition, index out of range");
            return false;
        }

        var branch = new BlockBranch();
        branch.Condition.TrySetBlock(condition);
        InsertBranch(branch, index);
        return true;
    }



    private void InsertBranch(BlockBranch branch, int index)
    {
        _branches.Insert(index, branch);
        SubscribeToBranch(branch);

        BranchInserted?.Invoke(branch, index);
        RaiseChanged();
    }



    public void RemoveBranch(BlockBranch branch)
    {
        if (!_branches.Remove(branch)) return;
        UnsubscribeFromBranch(branch);

        BranchRemoved?.Invoke(branch);
        RaiseChanged();
        branch.Dispose();
    }



    public override void RemoveBlock(BlockModel block)
    {
        foreach (var branch in _branches.ToArray())
        {
            branch.Condition.RemoveBlock(block);
            branch.Then.RemoveBlock(block);
        }
    }



    // running



    // runs the 'then' of the first branch whose condition is true, returns false if none matched
    public async Task<bool> RunFirstMatchAsync(TriggerContext context)
    {
        foreach (var branch in _branches.ToArray())
        {
            if (!await branch.Condition.EvaluateAsync(context)) continue;

            await branch.Then.RunAsync(context);
            return true;
        }

        return false;
    }



    // model callbacks



    private void OnBranchSlotChanged() => RaiseChanged();



    // a branch with nothing left in it is removed
    private void OnBranchBlockChanged(BlockModel? _, BlockModel? __)
    {
        foreach (var branch in _branches.ToArray())
            if (branch.IsEmpty) RemoveBranch(branch);
    }



    // helpers



    private void SubscribeToBranch(BlockBranch branch)
    {
        branch.Condition.Changed += OnBranchSlotChanged;
        branch.Then.Changed += OnBranchSlotChanged;
        branch.Condition.BlockChanged += OnBranchBlockChanged;
        branch.Then.BlockChanged += OnBranchBlockChanged;
    }



    private void UnsubscribeFromBranch(BlockBranch branch)
    {
        branch.Condition.Changed -= OnBranchSlotChanged;
        branch.Then.Changed -= OnBranchSlotChanged;
        branch.Condition.BlockChanged -= OnBranchBlockChanged;
        branch.Then.BlockChanged -= OnBranchBlockChanged;
    }



    // json (owners save the list through these)



    public List<BlockBranchData> GetData() => _branches.Select(b => b.GetData()).ToList();



    public void LoadData(List<BlockBranchData> data)
    {
        foreach (var item in data)
        {
            var branch = new BlockBranch();
            branch.LoadData(item);

            if (branch.IsEmpty)
            {
                branch.Dispose();
                continue;
            }

            InsertBranch(branch, _branches.Count);
        }
    }



    // disposal



    public override void Dispose()
    {
        foreach (var branch in _branches)
            branch.Dispose();
    }
}
