#nullable enable

using System;
using Godot;

public partial class LogWindow : Window
{
    internal static readonly Color WarningColor = new("e6c55f");
    internal static readonly Color ErrorColor = new("e35d6a");
    internal static readonly Color FatalColor = new("ff3b4f");

    private OsLogger? _logger;
    private RichTextLabel? _output;
    private bool _hasEntries;

    public event Action<string>? FatalErrorLogged;

    public override void _Ready()
    {
        _output = GetNode<RichTextLabel>("%LogOutput");
        CloseRequested += Hide;
    }

    public override void _Process(double delta)
    {
        _ = delta;
        if (_logger is null || _output is null)
            return;

        foreach (OsLogEntry entry in _logger.DrainPendingEntries())
            Append(entry);
    }

    public override void _ExitTree()
    {
        CloseRequested -= Hide;
        FatalErrorLogged = null;
        _logger = null;
        _output = null;
    }

    public void Bind(OsLogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public void ShowDialog()
    {
        PopupCentered();
    }

    private void Append(OsLogEntry entry)
    {
        if (_output is null)
            return;

        if (!_hasEntries)
        {
            _output.Clear();
            _hasEntries = true;
        }

        Color color = entry.Severity switch
        {
            OsLogSeverity.Warning => WarningColor,
            OsLogSeverity.Fatal => FatalColor,
            _ => ErrorColor,
        };
        string prefix = entry.Severity switch
        {
            OsLogSeverity.Warning => "Warning: ",
            OsLogSeverity.Fatal => "FATAL: ",
            _ => "Error: ",
        };

        _output.PushColor(color);
        _output.AddText(prefix);
        _output.AddText(entry.Message);
        _output.AddText("\n\n");
        _output.Pop();

        if (entry.Severity == OsLogSeverity.Fatal)
            FatalErrorLogged?.Invoke(entry.Message);
    }
}
