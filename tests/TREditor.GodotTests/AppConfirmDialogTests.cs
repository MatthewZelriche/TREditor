using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace TREditor.GodotTests;

[TestSuite]
public class AppConfirmDialogTests
{
    [TestCase]
    [RequireGodotRuntime]
    public void ConfirmationAndWindowCloseEmitTheExpectedSignals()
    {
        PackedScene dialogScene = ResourceLoader.Load<PackedScene>(
            "res://src/ui/window/AppConfirmDialog.tscn"
        );
        AppConfirmDialog dialog = AutoFree(
            dialogScene.Instantiate<AppConfirmDialog>()
        );
        dialog.DialogText = "Replace the existing value?";
        dialog.ConfirmButtonText = "Replace";
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(dialog);

        int confirmedCount = 0;
        int canceledCount = 0;
        dialog.Confirmed += () => confirmedCount++;
        dialog.Canceled += () => canceledCount++;

        AssertBool(dialog.Borderless).IsTrue();
        AssertBool(dialog.Unresizable).IsTrue();
        AssertThat(dialog.GetNode<Label>("%MessageLabel").Text)
            .IsEqual("Replace the existing value?");
        AssertThat(dialog.GetNode<Button>("%ConfirmButton").Text).IsEqual("Replace");

        dialog.PopupCentered();
        dialog
            .GetNode<Button>("%ConfirmButton")
            .EmitSignal(Button.SignalName.Pressed);
        AssertThat(confirmedCount).IsEqual(1);
        AssertThat(canceledCount).IsEqual(0);
        AssertBool(dialog.Visible).IsFalse();

        dialog.PopupCentered();
        dialog
            .GetNode<WindowTitleBar>("WindowTitleBar")
            .GetNode<Button>("%CloseButton")
            .EmitSignal(Button.SignalName.Pressed);
        AssertThat(confirmedCount).IsEqual(1);
        AssertThat(canceledCount).IsEqual(1);
        AssertBool(dialog.Visible).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void FixedConfirmationWindowOnlyShowsTheCloseControl()
    {
        PackedScene dialogScene = ResourceLoader.Load<PackedScene>(
            "res://src/ui/window/AppConfirmDialog.tscn"
        );
        AppConfirmDialog dialog = AutoFree(
            dialogScene.Instantiate<AppConfirmDialog>()
        );
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(dialog);
        WindowTitleBar titleBar = dialog.GetNode<WindowTitleBar>("WindowTitleBar");

        AssertBool(titleBar.GetNode<Button>("%MinimizeButton").Visible).IsFalse();
        AssertBool(titleBar.GetNode<Button>("%MaximizeButton").Visible).IsFalse();
        AssertBool(titleBar.GetNode<Button>("%CloseButton").Visible).IsTrue();
    }
}
