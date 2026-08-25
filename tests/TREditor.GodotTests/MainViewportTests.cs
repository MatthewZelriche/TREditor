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
}
