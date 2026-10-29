using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Blocks.Model;



// holds at most one block
public sealed class BlockSlot : BlockSlotBase
{
    // private



    private BlockModel? _block;



    // public



    public BlockModel? Block => _block;
    public override IReadOnlyList<BlockModel> Blocks => _block == null ? [] : [_block];



    // events



    public event Action<BlockModel?, BlockModel?>? BlockChanged;



    // constructor



    public BlockSlot(string label, Type acceptedType) : base(label, acceptedType)
    {
        _block = null;
    }



    // block management



    public bool TrySetBlock(BlockModel? block)
    {
        if (block != null && !Accepts(block))
        {
            AppLog.WriteError($"Could not place '{block.Name}' - {LockReason ?? "it doesn't fit in this slot"}");
            return false;
        }

        SetBlock(block);
        return true;
    }



    public override void RemoveBlock(BlockModel block)
    {
        if (_block != block) return;
        SetBlock(null);
    }



    private void SetBlock(BlockModel? newBlock)
    {
        if (_block == newBlock) return;

        var oldBlock = _block;
        if (oldBlock != null) Detach(oldBlock);

        _block = newBlock;
        if (_block != null) Attach(_block);

        BlockChanged?.Invoke(oldBlock, _block);
        RaiseChanged();
        oldBlock?.Dispose();
    }



    // running



    // an empty slot does nothing
    public Task RunAsync(TriggerContext context) =>
        (_block as ExecutableBlock)?.RunAsync(context) ?? Task.CompletedTask;



    // an empty condition slot counts as false
    public Task<bool> EvaluateAsync(TriggerContext context) =>
        (_block as ConditionBlock)?.EvaluateAsync(context) ?? Task.FromResult(false);



    // json (owners save the slot through these)



    public PendingBlockData? GetData() => _block == null ? null : BlockResolver.GetData(_block);



    public void LoadData(PendingBlockData? data)
    {
        var block = data == null ? null : BlockResolver.Resolve(data);
        if (!TrySetBlock(block)) block?.Dispose();
    }



    // disposal



    public override void Dispose() => _block?.Dispose();
}
