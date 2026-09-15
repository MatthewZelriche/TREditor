using System;

// Recoverable command failure: the document was not left in a broken invariant, but the command
// could not apply (for example a stale expected object).
public sealed class NonFatalCommandError(string message) : Exception(message);
