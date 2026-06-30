using System.Threading.Tasks;

namespace StreamAssist.Core;

/// <summary>
/// Anything can be bound to it. Base class for any action triggerable by the keyboard or chatbot.
/// </summary>
public interface IFeatureAction
{
    string Name        { get; }
    string Description { get; }
    Task ExecuteAsync();
}
