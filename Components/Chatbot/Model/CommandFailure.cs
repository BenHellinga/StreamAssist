namespace StreamAssist.Components.Chatbot.Model;



// why ChatbotComponentModel's TryAddCommand / TryEditCommand / TryDeleteCommand failed, so a chat command can say so
public enum CommandFailure
{
    None,
    Invalid, // no name, two words, no response, or no Match Text in the custom commands
    NotFound, // no such command (or it leads into more blocks, so it isn't one command)
    AlreadyExists, // adding one that's already there
    NotSimple, // editing one that does more than send a typed message
}
