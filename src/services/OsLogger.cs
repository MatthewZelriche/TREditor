#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;

internal enum OsLogSeverity
{
    Warning,
    Error,
    Fatal,
}

internal readonly record struct OsLogEntry(OsLogSeverity Severity, string Message);

public partial class OsLogger : Logger
{
    private readonly object _gate = new();
    private List<OsLogEntry> _pendingEntries = [];

    public override void _LogError(
        string function,
        string file,
        int line,
        string code,
        string rationale,
        bool editorNotify,
        int errorType,
        Array<ScriptBacktrace> scriptBacktraces
    )
    {
        _ = editorNotify;

        bool isUncaughtException = IsUncaughtCsharpException(function, file);
        OsLogSeverity severity = isUncaughtException
            ? OsLogSeverity.Fatal
            : (ErrorType)errorType == ErrorType.Warning
                ? OsLogSeverity.Warning
                : OsLogSeverity.Error;
        string message = GetMessage(function, file, line, code, rationale);

        Enqueue(severity, message);
    }

    public override void _LogMessage(string message, bool error)
    {
        // Error callbacks may also be mirrored to stderr. _LogError is the structured source of
        // warnings and errors, so accepting stderr here would produce duplicate log entries.
    }

    internal IReadOnlyList<OsLogEntry> DrainPendingEntries()
    {
        lock (_gate)
        {
            if (_pendingEntries.Count == 0)
                return [];

            List<OsLogEntry> entries = _pendingEntries;
            _pendingEntries = [];
            return entries;
        }
    }

    internal void LogFatal(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Enqueue(OsLogSeverity.Fatal, message.TrimEnd());
    }

    private void Enqueue(OsLogSeverity severity, string message)
    {
        lock (_gate)
            _pendingEntries.Add(new OsLogEntry(severity, message));
    }

    private static string GetMessage(
        string function,
        string file,
        int line,
        string code,
        string rationale
    )
    {
        // Godot's generated C# glue catches exceptions that escape user callbacks and forwards
        // them through ExceptionUtils. The exception, including its stack trace, is stored in code.
        if (IsUncaughtCsharpException(function, file) && !string.IsNullOrWhiteSpace(code))
            return code.TrimEnd();

        string message = string.IsNullOrWhiteSpace(rationale) ? code : rationale;
        if (!string.IsNullOrWhiteSpace(message))
            return message.TrimEnd();

        string location = string.IsNullOrWhiteSpace(file)
            ? function
            : $"{file}:{line} in {function}";
        return string.IsNullOrWhiteSpace(location) ? "Unknown error" : location;
    }

    private static bool IsUncaughtCsharpException(string function, string file)
    {
        if (
            function.Contains("ExceptionUtils.LogException", StringComparison.Ordinal)
            || function.Contains(
                "ExceptionUtils.LogUnhandledException",
                StringComparison.Ordinal
            )
        )
        {
            return true;
        }

        return file.Contains("ExceptionUtils.cs", StringComparison.Ordinal)
            && (
                function.Contains("LogException", StringComparison.Ordinal)
                || function.Contains("LogUnhandledException", StringComparison.Ordinal)
            );
    }
}
