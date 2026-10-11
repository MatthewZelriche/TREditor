using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace TREditor.GodotTests;

[TestSuite]
public class ToolsToolbarTests
{
    [TestCase]
    [RequireGodotRuntime]
    public void BottomBarXRayButtonControlsGlobalSelectionMode()
    {
        PackedScene rootScene = ResourceLoader.Load<PackedScene>("res://src/ui/Root.tscn");
        Root root = AutoFree(rootScene.Instantiate<Root>());
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(root);

        Application application = root.CurrentApplication!;
        Button button = root.GetNode<Button>(
            "UI/BottomBar/BarMargin/Controls/XRayButton"
        );

        AssertBool(application.SelectionMode.XRayEnabled).IsFalse();
        button.EmitSignal(BaseButton.SignalName.Pressed);
        AssertBool(application.SelectionMode.XRayEnabled).IsTrue();
        button.EmitSignal(BaseButton.SignalName.Pressed);
        AssertBool(application.SelectionMode.XRayEnabled).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void PressingThePrimitiveButtonAgainReturnsToTheDefaultTool()
    {
        PackedScene rootScene = ResourceLoader.Load<PackedScene>("res://src/ui/Root.tscn");
        Root root = AutoFree(rootScene.Instantiate<Root>());
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(root);

        Application application = root.CurrentApplication!;
        Button button = root.GetNode<Button>("UI/Toolbar/ToolbarMargin/Tools/NewPrimitiveButton");

        AssertThat(application.Tools.ActiveTool?.Id).IsEqual(TranslateTool.ToolId);
        AssertBool(button.ButtonPressed).IsFalse();

        button.EmitSignal(BaseButton.SignalName.Pressed);
        AssertThat(application.Tools.ActiveTool?.Id).IsEqual(PrimitiveTool.ToolId);
        AssertBool(button.ButtonPressed).IsTrue();

        button.EmitSignal(BaseButton.SignalName.Pressed);
        AssertThat(application.Tools.ActiveTool?.Id).IsEqual(TranslateTool.ToolId);
        AssertBool(button.ButtonPressed).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void MergeVerticesToolIsAvailableInVertexSelectionMode()
    {
        PackedScene rootScene = ResourceLoader.Load<PackedScene>("res://src/ui/Root.tscn");
        Root root = AutoFree(rootScene.Instantiate<Root>());
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(root);

        Application application = root.CurrentApplication!;
        Button button = root.GetNode<Button>(
            "UI/Toolbar/ToolbarMargin/Tools/MergeVerticesButton"
        );

        AssertBool(button.Visible).IsFalse();
        application.SelectionMode.ToggleComponent(ComponentMask.Vertex);
        AssertBool(button.Visible).IsTrue();

        button.EmitSignal(BaseButton.SignalName.Pressed);
        AssertThat(application.Tools.ActiveTool?.Id).IsEqual(MergeVerticesTool.ToolId);
        AssertBool(button.ButtonPressed).IsTrue();

        button.EmitSignal(BaseButton.SignalName.Pressed);
        AssertThat(application.Tools.ActiveTool?.Id).IsEqual(TranslateTool.ToolId);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void CollapseEdgesToolIsAvailableInEdgeSelectionMode()
    {
        PackedScene rootScene = ResourceLoader.Load<PackedScene>("res://src/ui/Root.tscn");
        Root root = AutoFree(rootScene.Instantiate<Root>());
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(root);

        Application application = root.CurrentApplication!;
        Button button = root.GetNode<Button>(
            "UI/Toolbar/ToolbarMargin/Tools/CollapseEdgesButton"
        );

        AssertBool(button.Visible).IsFalse();
        application.SelectionMode.ToggleComponent(ComponentMask.Edge);
        AssertBool(button.Visible).IsTrue();

        button.EmitSignal(BaseButton.SignalName.Pressed);
        AssertThat(application.Tools.ActiveTool?.Id).IsEqual(CollapseEdgesTool.ToolId);
        AssertBool(button.ButtonPressed).IsTrue();

        button.EmitSignal(BaseButton.SignalName.Pressed);
        AssertThat(application.Tools.ActiveTool?.Id).IsEqual(TranslateTool.ToolId);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ExtendEdgesToolIsAvailableInEdgeSelectionMode()
    {
        PackedScene rootScene = ResourceLoader.Load<PackedScene>("res://src/ui/Root.tscn");
        Root root = AutoFree(rootScene.Instantiate<Root>());
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(root);

        Application application = root.CurrentApplication!;
        Button button = root.GetNode<Button>(
            "UI/Toolbar/ToolbarMargin/Tools/ExtendEdgesButton"
        );

        AssertBool(button.Visible).IsFalse();
        application.SelectionMode.ToggleComponent(ComponentMask.Edge);
        AssertBool(button.Visible).IsTrue();

        button.EmitSignal(BaseButton.SignalName.Pressed);
        AssertThat(application.Tools.ActiveTool?.Id).IsEqual(ExtendEdgesTool.ToolId);
        AssertBool(button.ButtonPressed).IsTrue();

        button.EmitSignal(BaseButton.SignalName.Pressed);
        AssertThat(application.Tools.ActiveTool?.Id).IsEqual(TranslateTool.ToolId);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void CollapseFacesToolIsAvailableInFaceSelectionMode()
    {
        PackedScene rootScene = ResourceLoader.Load<PackedScene>("res://src/ui/Root.tscn");
        Root root = AutoFree(rootScene.Instantiate<Root>());
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(root);

        Application application = root.CurrentApplication!;
        Button button = root.GetNode<Button>(
            "UI/Toolbar/ToolbarMargin/Tools/CollapseFacesButton"
        );

        AssertBool(button.Visible).IsFalse();
        application.SelectionMode.ToggleComponent(ComponentMask.Face);
        AssertBool(button.Visible).IsTrue();

        button.EmitSignal(BaseButton.SignalName.Pressed);
        AssertThat(application.Tools.ActiveTool?.Id).IsEqual(CollapseFacesTool.ToolId);
        AssertBool(button.ButtonPressed).IsTrue();

        button.EmitSignal(BaseButton.SignalName.Pressed);
        AssertThat(application.Tools.ActiveTool?.Id).IsEqual(TranslateTool.ToolId);
    }
}
