using System.Threading.Tasks;

namespace StreamAssist.Core.Models;

/// <summary>
/// Anything a key can be bound to. When you implement drag-and-drop key binding,
/// each key will store an IStreamAction? and invoke it on press.
///
/// Examples of concrete implementations you'll make later:
///   SwitchObsSceneAction, PlaySoundAction, SendChatMessageAction, etc.
/// </summary>
public interface IStreamAction
{
    string Name        { get; }
    string Description { get; }
    Task ExecuteAsync();
}
