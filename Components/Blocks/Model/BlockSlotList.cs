using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Blocks.Model;



// holds any number of blocks in order
public sealed class BlockSlotList : BlockSlotBase
{
    // private



    private readonly List<BlockModel> _blocks;



    // public



    public override IReadOnlyList<BlockModel> Blocks => _blocks;



    // events



    public event Action<BlockModel, int>? BlockInserted;
    public event Action<BlockModel>? BlockRemoved;



    // constructor



    public BlockSlotList(string label, Type acceptedType) : base(label, acceptedType)
    {
        _blocks = [];
    }



    // block management



    public bool TryInsertBlock(BlockModel block, int index)
    {
        if (!Accepts(block))
        {
            AppLog.WriteError($"Could not place '{block.Name}' - {LockReason ?? "it doesn't fit in this list"}");
            return false;
        }

        if (index < 0 || index > _blocks.Count)
        {
            AppLog.WriteError("Could not place block - index out of range");
            return false;
        }

        InsertBlock(block, index);
        return true;
    }



    private void InsertBlock(BlockModel block, int index)
    {
        _blocks.Insert(index, block);
        Attach(block);

        BlockInserted?.Invoke(block, index);
        RaiseChanged();
    }



    public override void RemoveBlock(BlockModel block)
    {
        if (!_blocks.Remove(block)) return;
        Detach(block);

        BlockRemoved?.Invoke(block);
        RaiseChanged();
        block.Dispose();
    }



    // running



    // runs each block in order, waiting for one to finish before starting the next
    public async Task RunAllAsync(TriggerContext context)
    {
        // copy so blocks added/removed while running don't break the loop
        foreach (var block in _blocks.ToArray())
            if (block is ExecutableBlock executable)
                await executable.RunAsync(context);
    }



    // json (owners save the list through these)



    public List<PendingBlockData> GetData() => _blocks.Select(BlockResolver.GetData).ToList();



    public void LoadData(List<PendingBlockData> data)
    {
        foreach (var item in data)
        {
            var block = BlockResolver.Resolve(item);
            if (block == null) continue;

            if (!TryInsertBlock(block, _blocks.Count))
                block.Dispose();
        }
    }



    // disposal



    public override void Dispose()
    {
        foreach (var block in _blocks)
            block.Dispose();
    }
}
