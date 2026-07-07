using System.Threading.Tasks;

namespace StreamAssist.Components.StreamAction;

/// <summary>
/// Anything can be bound to it. Base class for any action triggerable by the keyboard or chatbot.
/// </summary>
public interface IStreamAction
{
    string Name        { get; }
    string Description { get; }
    Task SafeExecuteAsync();
}
