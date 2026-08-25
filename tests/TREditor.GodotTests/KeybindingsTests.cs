using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace TREditor.GodotTests;

[TestSuite]
public class KeybindingsTests
{
    private const string SettingsPath = "user://keybindings_test.cfg";

    [TestCase]
    [RequireGodotRuntime]
    public void BindingsPersistAndExplicitReplacementUnsetsTheConflict()
    {
        RemoveSettingsFile();
        using var binding = new InputEventKey
        {
            PhysicalKeycode = Key.I,
            CtrlPressed = true,
            ShiftPressed = true,
        };
        using var keybindings = new KeybindingManager(SettingsPath);

        try
        {
            keybindings.Load();
            KeybindingAssignmentResult assigned = keybindings.Assign(
                EditorInputActions.CameraMoveForward,
                binding
            );
            AssertBool(assigned.Applied).IsTrue();

            using var loaded = new KeybindingManager(SettingsPath);
            loaded.Load();
            AssertBool(
                loaded
                    .GetBinding(EditorInputActions.CameraMoveForward)!
                    .IsMatch(binding, exactMatch: true)
            ).IsTrue();

            using var unmodifiedI = new InputEventKey
            {
                PhysicalKeycode = Key.I,
            };
            KeybindingAssignmentResult distinctModifiers = loaded.Assign(
                EditorInputActions.CameraMoveBackward,
                unmodifiedI
            );
            AssertBool(distinctModifiers.Applied).IsTrue();

            KeybindingAssignmentResult conflict = loaded.Assign(
                EditorInputActions.CameraMoveBackward,
                binding
            );
            AssertBool(conflict.Applied).IsFalse();
            AssertThat(conflict.ConflictingActionName)
                .IsEqual(EditorInputActions.CameraMoveForward);

            KeybindingAssignmentResult replaced = loaded.Assign(
                EditorInputActions.CameraMoveBackward,
                binding,
                replaceConflict: true
            );
            AssertBool(replaced.Applied).IsTrue();
            AssertThat(loaded.GetBinding(EditorInputActions.CameraMoveForward)).IsNull();
            AssertBool(
                loaded
                    .GetBinding(EditorInputActions.CameraMoveBackward)!
                    .IsMatch(binding, exactMatch: true)
            ).IsTrue();

            Godot.Collections.Array<InputEvent> events = InputMap.ActionGetEvents(
                EditorInputActions.CameraMoveBackward
            );
            AssertThat(events.Count).IsEqual(1);
            var inputEvent = (InputEventKey)events[0];
            AssertThat(inputEvent.PhysicalKeycode).IsEqual(Key.I);
            AssertBool(inputEvent.CtrlPressed).IsTrue();
            AssertBool(inputEvent.ShiftPressed).IsTrue();
        }
        finally
        {
            keybindings.ResetDefaults();
            RemoveSettingsFile();
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void SettingsMenuOpensKeybindingsDialog()
    {
        PackedScene rootScene = ResourceLoader.Load<PackedScene>("res://src/ui/Root.tscn");
        Root root = AutoFree(rootScene.Instantiate<Root>());
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(root);

        AppTitleBar titleBar = root.GetNode<AppTitleBar>("UI/AppTitleBar");
        PopupMenu settingsMenu = titleBar.GetNode<PopupMenu>("%SettingsMenu");
        KeybindingsDialog dialog = root.GetNode<KeybindingsDialog>("%KeybindingsDialog");

        AssertBool(dialog.Visible).IsFalse();
        settingsMenu.EmitSignal(
            PopupMenu.SignalName.IdPressed,
            (long)AppTitleBar.AppMenuAction.Keybindings
        );

        AssertBool(dialog.Visible).IsTrue();
        dialog.Hide();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DialogCapturesModifiersAndConfirmsConflictReplacement()
    {
        RemoveSettingsFile();
        using var keybindings = new KeybindingManager(SettingsPath);
        keybindings.Load();
        PackedScene dialogScene = ResourceLoader.Load<PackedScene>(
            "res://src/ui/settings/KeybindingsDialog.tscn"
        );
        KeybindingsDialog dialog = AutoFree(dialogScene.Instantiate<KeybindingsDialog>());
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(dialog);

        try
        {
            dialog.ShowDialog(keybindings);
            dialog.BeginCapture(EditorInputActions.CameraMoveForward);
            using var controlI = new InputEventKey
            {
                PhysicalKeycode = Key.I,
                CtrlPressed = true,
                Pressed = true,
            };
            dialog._Input(controlI);
            AssertBool(
                keybindings
                    .GetBinding(EditorInputActions.CameraMoveForward)!
                    .IsMatch(controlI, exactMatch: true)
            ).IsTrue();

            dialog.BeginCapture(EditorInputActions.CameraMoveBackward);
            dialog._Input(controlI);
            AssertBool(dialog.ConflictDialog.Visible).IsTrue();
            dialog.ConflictDialog.EmitSignal(AcceptDialog.SignalName.Confirmed);

            AssertThat(keybindings.GetBinding(EditorInputActions.CameraMoveForward)).IsNull();
            AssertBool(
                keybindings
                    .GetBinding(EditorInputActions.CameraMoveBackward)!
                    .IsMatch(controlI, exactMatch: true)
            ).IsTrue();
        }
        finally
        {
            keybindings.ResetDefaults();
            RemoveSettingsFile();
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void CameraMovementUsesTheReboundInputAction()
    {
        RemoveSettingsFile();
        using var keybindings = new KeybindingManager(SettingsPath);
        keybindings.Load();
        using var binding = new InputEventKey
        {
            PhysicalKeycode = Key.I,
            CtrlPressed = true,
        };
        keybindings.Assign(EditorInputActions.CameraMoveForward, binding);

        MainViewport viewport = AutoFree(new MainViewport());
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(viewport);
        using var rightPress = new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Right,
            Pressed = true,
        };
        viewport.HandleGuiInput(rightPress);
        Vector3 initialPosition = viewport.Camera.Position;

        using var inputPress = new InputEventKey
        {
            PhysicalKeycode = Key.I,
            CtrlPressed = true,
            Pressed = true,
        };
        try
        {
            AssertBool(
                InputMap.EventIsAction(
                    inputPress,
                    EditorInputActions.CameraMoveForward,
                    exactMatch: true
                )
            ).IsTrue();
            Input.ActionPress(EditorInputActions.CameraMoveForward);
            viewport._Process(1.0);
            AssertBool(!viewport.Camera.Position.IsEqualApprox(initialPosition)).IsTrue();
        }
        finally
        {
            Input.ActionRelease(EditorInputActions.CameraMoveForward);
            keybindings.ResetDefaults();
            RemoveSettingsFile();
        }
    }

    private static void RemoveSettingsFile()
    {
        if (Godot.FileAccess.FileExists(SettingsPath))
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(SettingsPath));
    }
}
