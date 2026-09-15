using System;

// Recoverable scene file failure: missing, unreadable, or invalid external data.
public sealed class NonFatalSceneError(string message) : Exception(message);
