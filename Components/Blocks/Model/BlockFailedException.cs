using System;

namespace StreamAssist.Components.Blocks.Model;



// thrown by a block that couldn't go on, after it has already logged why (it knows the most about what went wrong)
// it stops the run like any other error, but the root (SafeRunAsync) doesn't log it a second time
public sealed class BlockFailedException(string reason) : Exception(reason);
