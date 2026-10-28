using System.Threading;

namespace StreamAssist.Components.Blocks.Model;



// Start/Stop come from things that are held (key down/up), Once from instant triggers (chat command, timer)
public enum TriggerPhase { Start, Stop, Once }



// passed down the block tree on every run
public class TriggerContext(TriggerPhase phase, CancellationToken cancellationToken)
{
    public TriggerPhase Phase { get; } = phase;
    public CancellationToken CancellationToken { get; } = cancellationToken;
}
