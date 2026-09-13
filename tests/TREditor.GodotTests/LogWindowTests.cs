using GdUnit4;
using Godot;
using Godot.Collections;
using static GdUnit4.Assertions;

namespace TREditor.GodotTests;

[TestSuite]
public class LogWindowTests
{
    [TestCase]
    [RequireGodotRuntime]
    public void AboutMenuOpensTheCustomLogWindow()
    {
        PackedScene rootScene = ResourceLoader.Load<PackedScene>("res://src/ui/Root.tscn");
        Root root = AutoFree(rootScene.Instantiate<Root>());
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(root);

        PopupMenu aboutMenu = root
            .GetNode<AppMenuBar>("UI/AppMenuBar")
            .GetNode<PopupMenu>("%AboutMenu");
        LogWindow window = root.GetNode<LogWindow>("%LogWindow");

        AssertBool(window.Visible).IsFalse();
        aboutMenu.EmitSignal(
            PopupMenu.SignalName.IdPressed,
            (long)AppMenuBar.AppMenuAction.Logs
        );

        AssertBool(window.Visible).IsTrue();
        AssertBool(window.Borderless).IsTrue();
        AssertBool(window.Unresizable).IsFalse();
        AssertBool(window.GetNode<RichTextLabel>("%LogOutput").ScrollActive).IsTrue();
        AssertBool(
            window.GetNode<WindowTitleBar>("WindowTitleBar")
                .GetNode<Button>("%CloseButton")
                .Visible
        ).IsTrue();
        window.Hide();

        root.CurrentLogger!._LogError(
            "Godot.NativeInterop.ExceptionUtils.LogUnhandledException",
            "ExceptionUtils.cs",
            30,
            "System.InvalidOperationException: fatal test exception",
            string.Empty,
            false,
            (int)Logger.ErrorType.Error,
            new Array<ScriptBacktrace>()
        );
        window._Process(0.0);

        AcceptDialog dialog = root.GetNode<AcceptDialog>("%FatalErrorDialog");
        AssertBool(dialog.Visible).IsTrue();
        AssertBool(dialog.DialogText.Contains("fatal test exception")).IsTrue();
        AssertThat(dialog.GetOkButton().Text).IsEqual("Close Application");
        dialog.Hide();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void LoggerQueuesWarningsErrorsAndUncaughtExceptions()
    {
        using var logger = new OsLogger();

        logger._LogError(
            "TestWarning",
            "Test.cs",
            10,
            "warning text",
            string.Empty,
            false,
            (int)Logger.ErrorType.Warning,
            new Array<ScriptBacktrace>()
        );
        logger._LogError(
            "TestError",
            "Test.cs",
            20,
            "error text",
            string.Empty,
            false,
            (int)Logger.ErrorType.Error,
            new Array<ScriptBacktrace>()
        );
        logger._LogError(
            "Godot.NativeInterop.ExceptionUtils.LogUnhandledException",
            "ExceptionUtils.cs",
            30,
            "System.InvalidOperationException: uncaught text\n   at Test()",
            string.Empty,
            false,
            (int)Logger.ErrorType.Error,
            new Array<ScriptBacktrace>()
        );

        var entries = logger.DrainPendingEntries();
        AssertThat(entries.Count).IsEqual(3);
        AssertThat((int)entries[0].Severity).IsEqual((int)OsLogSeverity.Warning);
        AssertThat(entries[0].Message).IsEqual("warning text");
        AssertThat((int)entries[1].Severity).IsEqual((int)OsLogSeverity.Error);
        AssertThat(entries[1].Message).IsEqual("error text");
        AssertThat((int)entries[2].Severity).IsEqual((int)OsLogSeverity.Fatal);
        AssertBool(entries[2].Message.Contains("uncaught text")).IsTrue();
        AssertBool(entries[2].Message.Contains("at Test()")).IsTrue();
        AssertThat(logger.DrainPendingEntries().Count).IsEqual(0);
    }

}
