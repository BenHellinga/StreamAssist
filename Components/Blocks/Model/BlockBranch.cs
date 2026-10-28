using System;
using System.Text.Json.Serialization;

namespace StreamAssist.Components.Blocks.Model;



// one "if <condition> then <block>" pair inside a BlockBranchList
public sealed class BlockBranch : IDisposable
{
    // private



    private readonly BlockSlot _condition;
    private readonly BlockSlot _then;



    // public



    public BlockSlot Condition => _condition;
    public BlockSlot Then => _then;
    public bool IsEmpty => _condition.Block == null && _then.Block == null;



    // constructor



    public BlockBranch()
    {
        _condition = new("", typeof(ConditionBlock));
        _then = new("", typeof(ExecutableBlock));
    }



    // json



    public BlockBranchData GetData() => new()
    {
        Condition = _condition.GetData(),
        Then = _then.GetData()
    };



    public void LoadData(BlockBranchData data)
    {
        _condition.LoadData(data.Condition);
        _then.LoadData(data.Then);
    }



    // disposal



    public void Dispose()
    {
        _condition.Dispose();
        _then.Dispose();
    }
}



// how a branch is saved
public class BlockBranchData
{
    [JsonPropertyName("condition")]
    public PendingBlockData? Condition { get; set; }

    [JsonPropertyName("then")]
    public PendingBlockData? Then { get; set; }
}
