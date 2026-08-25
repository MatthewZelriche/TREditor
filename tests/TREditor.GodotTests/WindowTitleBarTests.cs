using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace TREditor.GodotTests;

[TestSuite]
public class WindowTitleBarTests
{
    [TestCase]
    [RequireGodotRuntime]
    public void FixedWindowShowsOnlyCloseAndCanHideItsTitle()
    {
        Window window = AutoFree(new Window { Title = "Generic Window" });
        PackedScene titleBarScene = ResourceLoader.Load<PackedScene>(
            "res://src/ui/window/WindowTitleBar.tscn"
        );
        WindowTitleBar titleBar = titleBarScene.Instantiate<WindowTitleBar>();
        titleBar.Resizable = false;
        titleBar.ShowTitle = false;
        window.AddChild(titleBar);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(window);

        Button minimizeButton = titleBar.GetNode<Button>("%MinimizeButton");
        Button maximizeButton = titleBar.GetNode<Button>("%MaximizeButton");
        Button closeButton = titleBar.GetNode<Button>("%CloseButton");
        Label windowTitle = titleBar.GetNode<Label>("%WindowTitle");

        AssertBool(window.Unresizable).IsTrue();
        AssertBool(window.MinimizeDisabled).IsTrue();
        AssertBool(window.MaximizeDisabled).IsTrue();
        AssertBool(minimizeButton.Visible).IsFalse();
        AssertBool(maximizeButton.Visible).IsFalse();
        AssertBool(closeButton.Visible).IsTrue();
        AssertBool(windowTitle.Visible).IsFalse();

        bool closeRequested = false;
        window.CloseRequested += () => closeRequested = true;
        closeButton.EmitSignal(Button.SignalName.Pressed);
        AssertBool(closeRequested).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ResizableWindowConfiguresOptionalControlsAndTracksItsTitle()
    {
        Window window = AutoFree(new Window { Title = "Initial Title" });
        PackedScene titleBarScene = ResourceLoader.Load<PackedScene>(
            "res://src/ui/window/WindowTitleBar.tscn"
        );
        WindowTitleBar titleBar = titleBarScene.Instantiate<WindowTitleBar>();
        titleBar.ShowMinimizeButton = false;
        window.AddChild(titleBar);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(window);

        Button minimizeButton = titleBar.GetNode<Button>("%MinimizeButton");
        Button maximizeButton = titleBar.GetNode<Button>("%MaximizeButton");
        Label windowTitle = titleBar.GetNode<Label>("%WindowTitle");

        AssertBool(window.Unresizable).IsFalse();
        AssertBool(window.MinimizeDisabled).IsTrue();
        AssertBool(window.MaximizeDisabled).IsFalse();
        AssertBool(minimizeButton.Visible).IsFalse();
        AssertBool(maximizeButton.Visible).IsTrue();
        AssertThat(windowTitle.Text).IsEqual("Initial Title");

        window.Title = "Updated Title";
        AssertThat(windowTitle.Text).IsEqual("Updated Title");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void MaximizeButtonRestoresThePreviousWindowBounds()
    {
        var initialPosition = new Vector2I(120, 80);
        var initialSize = new Vector2I(640, 360);
        Window window = AutoFree(
            new Window
            {
                Position = initialPosition,
                Size = initialSize,
            }
        );
        PackedScene titleBarScene = ResourceLoader.Load<PackedScene>(
            "res://src/ui/window/WindowTitleBar.tscn"
        );
        WindowTitleBar titleBar = titleBarScene.Instantiate<WindowTitleBar>();
        window.AddChild(titleBar);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(window);

        Button maximizeButton = titleBar.GetNode<Button>("%MaximizeButton");
        maximizeButton.EmitSignal(Button.SignalName.Pressed);
        AssertThat(maximizeButton.TooltipText).IsEqual("Restore");

        maximizeButton.EmitSignal(Button.SignalName.Pressed);
        AssertThat(maximizeButton.TooltipText).IsEqual("Maximize");
        AssertThat(window.Mode).IsEqual(Window.ModeEnum.Windowed);
        AssertThat(window.Position).IsEqual(initialPosition);
        AssertThat(window.Size).IsEqual(initialSize);
    }
}
