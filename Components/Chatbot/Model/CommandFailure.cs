namespace StreamAssist.Components.Chatbot.Model;



// why ChatbotComponentModel's TryAddCommand / TryEditCommand / TryDeleteCommand failed, so a chat command can say so
public enum CommandFailure
{
    None,
    Invalid, // no name, two words, no response, or no chat config
    NotFound, // no such command (or it leads into more blocks, so it isn't one command)
    AlreadyExists, // adding one that's already there (anywhere in the tree)
    NotSimple, // editing one that does more than send a typed message
    NotEditable, // editing or deleting one that isn't in an Editable Commands block
    NoPlace, // adding one that no Editable Commands block fits
}
