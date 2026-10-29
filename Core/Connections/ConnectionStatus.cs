namespace StreamAssist.Core.Connections;



// the state of a connection to another program or service (Twitch, Voicemeeter, ...), shown in the Connections tab
public enum ConnectionStatus { NotConnected, Connecting, Connected, Failed }
