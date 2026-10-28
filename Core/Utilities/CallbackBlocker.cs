namespace StreamAssist.Core.Utilities;

public sealed class CallbackBlocker
{
    private int _count = 0;

    public void Block() => ++_count;
    public void Unblock() => _count = _count > 0 ? _count - 1 : 0;
    public bool IsBlocked() => _count != 0;
    public bool TryBlock()
    {
        if (_count != 0) return false;
        ++_count;
        return true;
    }
}