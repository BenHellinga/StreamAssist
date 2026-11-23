using System;
using System.ComponentModel;

namespace StreamAssist.Scripting;



/// <summary>
/// Something the app couldn't do (e.g. OBS isn't connected, there's no dock with that name). Uncaught, it ends the run
/// and its message is logged in the console; catch it to carry on instead.
/// </summary>
public class StreamAssistException : Exception
{
    /// <summary>True if the app already logged why (e.g. a Twitch request that failed), so it isn't logged again.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool IsLogged { get; }



    public StreamAssistException(string message) : this(message, false) { }



    [EditorBrowsable(EditorBrowsableState.Never)]
    public StreamAssistException(string message, bool isLogged) : base(message) => IsLogged = isLogged;
}



/// <summary>Thrown by Stop() to end a run quietly.</summary>
public sealed class ScriptStoppedException() : Exception("stopped");
