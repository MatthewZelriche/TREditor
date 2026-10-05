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
}
