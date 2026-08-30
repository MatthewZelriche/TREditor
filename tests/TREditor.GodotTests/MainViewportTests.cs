using System.Collections.Generic;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace TREditor.GodotTests;

[TestSuite]
public class MainViewportTests
{
    [TestCase]
    [RequireGodotRuntime]
    public void MainViewportAreaOwnsAndChangesTheActiveViewport()
    {
        var panes = new List<TilingContainerPane<MainViewport>>();
        var activeViewports = new List<MainViewport?>();
        MainViewportArea area = AutoFree(
            new MainViewportArea { Size = new Vector2(800.0f, 600.0f) }
        );
        area.ActiveViewportChanged += activeViewports.Add;
        area.ChildEnteredTree += child =>
        {
            if (child is TilingContainerPane<MainViewport> pane)
                panes.Add(pane);
        };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(area);

        AssertThat(panes.Count).IsEqual(1);
        MainViewport firstViewport = panes[0].Content;
        AssertThat(area.ActiveViewport).IsSame(firstViewport);

        TilingContainerPane<MainViewport> secondPane = area.CreatePane();
        AssertBool(
            area.InsertPaneSplit(
                panes[0],
                secondPane,
                SplitAxis.Horizontal,
                InsertPlacement.After
            )
        ).IsTrue();
        MainViewport secondViewport = secondPane.Content;

        using var leftPress = new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left,
            Pressed = true,
        };
        secondViewport.HandleGuiInput(leftPress);
        AssertThat(area.ActiveViewport).IsSame(secondViewport);
        AssertBool(secondViewport.IsNavigating).IsFalse();

        using var firstRightPress = new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Right,
            Pressed = true,
        };
        firstViewport.HandleGuiInput(firstRightPress);
        AssertThat(area.ActiveViewport).IsSame(firstViewport);
        AssertBool(firstViewport.IsNavigating).IsTrue();

        using var secondRightPress = new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Right,
            Pressed = true,
        };
        secondViewport.HandleGuiInput(secondRightPress);
        AssertThat(area.ActiveViewport).IsSame(secondViewport);
        AssertBool(firstViewport.IsNavigating).IsFalse();
        AssertBool(secondViewport.IsNavigating).IsTrue();

        AssertBool(area.RemoveLeaf(secondPane)).IsTrue();
        AssertThat(area.ActiveViewport).IsSame(firstViewport);
        AssertBool(secondViewport.IsNavigating).IsFalse();
        AssertThat(activeViewports.Count).IsEqual(5);
        AssertThat(activeViewports[^1]).IsSame(firstViewport);
        secondPane.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void RightMouseNavigatesWhileLeftMouseRemainsAvailable()
    {
        MainViewport viewport = AutoFree(
            new MainViewport
            {
                Position = Vector2.Zero,
                Size = new Vector2(640.0f, 480.0f),
            }
        );
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(viewport);
        Camera3D camera = viewport.Camera;
        Vector3 initialRotation = camera.Rotation;
        Vector3 initialPosition = camera.Position;

        using var leftPress = new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left,
            Pressed = true,
            Position = new Vector2(100.0f, 100.0f),
        };
        viewport.HandleGuiInput(leftPress);
        AssertBool(viewport.IsNavigating).IsFalse();

        using var rightPress = new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Right,
            Pressed = true,
            Position = new Vector2(100.0f, 100.0f),
        };
        viewport.HandleGuiInput(rightPress);
        AssertBool(viewport.IsNavigating).IsTrue();

        using var mouseMotion = new InputEventMouseMotion
        {
            Relative = new Vector2(20.0f, -10.0f),
        };
        viewport._Input(mouseMotion);
        AssertBool(!camera.Rotation.IsEqualApprox(initialRotation)).IsTrue();

        viewport.Move(Vector2.Up, 1.0);
        AssertBool(!camera.Position.IsEqualApprox(initialPosition)).IsTrue();

        using var rightRelease = new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Right,
            Pressed = false,
            Position = new Vector2(100.0f, 100.0f),
        };
        viewport._Input(rightRelease);
        AssertBool(viewport.IsNavigating).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void LeftMouseRaisesToolPhasesInOrder()
    {
        MainViewport viewport = AutoFree(
            new MainViewport
            {
                Position = Vector2.Zero,
                Size = new Vector2(640.0f, 480.0f),
            }
        );
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(viewport);
        var phases = new List<string>();
        var positions = new List<Vector2>();
        viewport.ToolMouseDown += input => Record("down", input);
        viewport.ToolMouseDrag += input => Record("drag", input);
        viewport.ToolMouseUp += input => Record("up", input);

        using var leftPress = new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left,
            Pressed = true,
            Position = new Vector2(10.0f, 20.0f),
            ShiftPressed = true,
        };
        viewport.HandleGuiInput(leftPress);
        AssertBool(viewport.IsToolDragging).IsTrue();
        AssertBool(viewport.IsNavigating).IsFalse();

        using var motion = new InputEventMouseMotion
        {
            Position = new Vector2(30.0f, 40.0f),
            Relative = new Vector2(20.0f, 20.0f),
        };
        viewport._Input(motion);

        using var leftRelease = new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left,
            Pressed = false,
            Position = new Vector2(50.0f, 60.0f),
        };
        viewport._Input(leftRelease);
        AssertBool(viewport.IsToolDragging).IsFalse();

        // Motion after the interaction ended belongs to nobody.
        viewport._Input(motion);

        AssertThat(string.Join(",", phases)).IsEqual("down,drag,up");
        AssertThat(positions[0]).IsEqual(new Vector2(10.0f, 20.0f));
        AssertThat(positions[1]).IsEqual(new Vector2(30.0f, 40.0f));
        AssertThat(positions[2]).IsEqual(new Vector2(50.0f, 60.0f));

        void Record(string phase, EditorMouseInput input)
        {
            phases.Add(phase);
            positions.Add(input.ScreenPosition);
            AssertThat(input.Camera).IsSame(viewport.Camera);
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void RightMouseNavigationRaisesNoToolPhases()
    {
        MainViewport viewport = AutoFree(
            new MainViewport
            {
                Position = Vector2.Zero,
                Size = new Vector2(640.0f, 480.0f),
            }
        );
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(viewport);
        var phases = new List<string>();
        viewport.ToolMouseDown += _ => phases.Add("down");
        viewport.ToolMouseDrag += _ => phases.Add("drag");
        viewport.ToolMouseUp += _ => phases.Add("up");

        using var rightPress = new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Right,
            Pressed = true,
            Position = new Vector2(100.0f, 100.0f),
        };
        viewport.HandleGuiInput(rightPress);
        using var motion = new InputEventMouseMotion { Relative = new Vector2(5.0f, 5.0f) };
        viewport._Input(motion);
        using var rightRelease = new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Right,
            Pressed = false,
            Position = new Vector2(100.0f, 100.0f),
        };
        viewport._Input(rightRelease);

        AssertBool(viewport.IsToolDragging).IsFalse();
        AssertThat(string.Join(",", phases)).IsEqual(string.Empty);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void PaneToolInputReachesTheActiveRegistryTool()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        var tool = new PhaseRecordingTool();
        application.Tools.Register(tool);
        application.Tools.Activate(PhaseRecordingTool.ToolId);

        var panes = new List<TilingContainerPane<MainViewport>>();
        MainViewportArea area = AutoFree(
            new MainViewportArea { Size = new Vector2(800.0f, 600.0f) }
        );
        area.ChildEnteredTree += child =>
        {
            if (child is TilingContainerPane<MainViewport> pane)
                panes.Add(pane);
        };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(area);
        area.Bind(application);

        TilingContainerPane<MainViewport> secondPane = area.CreatePane();
        AssertBool(
            area.InsertPaneSplit(
                panes[0],
                secondPane,
                SplitAxis.Horizontal,
                InsertPlacement.After
            )
        ).IsTrue();

        DragInViewport(panes[0].Content);
        DragInViewport(secondPane.Content);

        AssertThat(string.Join(",", tool.Phases)).IsEqual("down,drag,up,down,drag,up");
        AssertThat(tool.Cameras.Count).IsEqual(6);
        AssertThat(tool.Cameras[0]).IsSame(panes[0].Content.Camera);
        AssertThat(tool.Cameras[^1]).IsSame(secondPane.Content.Camera);

        // A pane that left the tree no longer feeds the registry.
        AssertBool(area.RemoveLeaf(secondPane)).IsTrue();
        using var pressAfterRemoval = new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left,
            Pressed = true,
            Position = new Vector2(15.0f, 25.0f),
        };
        secondPane.Content.HandleGuiInput(pressAfterRemoval);
        AssertThat(tool.Phases.Count).IsEqual(6);
        secondPane.Free();

        static void DragInViewport(MainViewport viewport)
        {
            using var press = new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left,
                Pressed = true,
                Position = new Vector2(15.0f, 25.0f),
            };
            viewport.HandleGuiInput(press);

            using var motion = new InputEventMouseMotion
            {
                Position = new Vector2(35.0f, 45.0f),
                Relative = new Vector2(20.0f, 20.0f),
            };
            viewport._Input(motion);

            using var release = new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left,
                Pressed = false,
                Position = new Vector2(35.0f, 45.0f),
            };
            viewport._Input(release);
        }
    }

    private sealed class PhaseRecordingTool : IEditorTool
    {
        public const string ToolId = "test.phase-recording";

        public string Id => ToolId;

        public List<string> Phases { get; } = [];

        public List<Camera3D> Cameras { get; } = [];

        public void OnMouseDown(IEditorToolContext context, EditorMouseInput input) =>
            Record("down", input);

        public void OnMouseDrag(IEditorToolContext context, EditorMouseInput input) =>
            Record("drag", input);

        public void OnMouseUp(IEditorToolContext context, EditorMouseInput input) =>
            Record("up", input);

        public void Cancel(IEditorToolContext context) => Phases.Add("cancel");

        private void Record(string phase, EditorMouseInput input)
        {
            Phases.Add(phase);
            Cameras.Add(input.Camera);
        }
    }
}
