#nullable enable

using System;
using System.Collections.Generic;
using Godot;

public sealed record KeybindingDefinition(
    string ActionId,
    string Category,
    string DisplayName,
    InputBinding? DefaultBinding
);

public static class KeybindingActions
{
    public const string FileNew = "editor_file_new";
    public const string FileOpen = "editor_file_open";
    public const string FileSave = "editor_file_save";
    public const string FileQuit = "editor_file_quit";
    public const string EditUndo = "editor_edit_undo";
    public const string EditRedo = "editor_edit_redo";
    public const string ToolSelect = "editor_tool_select";
    public const string ToolEdit = "editor_tool_edit";
    public const string ToolCreate = "editor_tool_create";
    public const string ToolTexture = "editor_tool_texture";
    public const string Confirm = "editor_confirm";
    public const string Cancel = "editor_cancel";
    public const string DeleteSelection = "editor_delete_selection";
    public const string CameraForward = "viewport_camera_forward";
    public const string CameraBack = "viewport_camera_back";
    public const string CameraLeft = "viewport_camera_left";
    public const string CameraRight = "viewport_camera_right";
    public const string CameraLook = "viewport_camera_look";
}

public static class KeybindingCatalog
{
    private static readonly InputBindingModifiers Ctrl = InputBindingModifiers.Ctrl;
    private static readonly InputBindingModifiers CtrlShift =
        InputBindingModifiers.Ctrl | InputBindingModifiers.Shift;

    private static readonly KeybindingDefinition[] Definitions =
    [
        new(KeybindingActions.FileNew, "File", "New", new KeyInputBinding(Key.N, Ctrl)),
        new(KeybindingActions.FileOpen, "File", "Open", new KeyInputBinding(Key.O, Ctrl)),
        new(KeybindingActions.FileSave, "File", "Save", new KeyInputBinding(Key.S, Ctrl)),
        new(KeybindingActions.FileQuit, "File", "Quit", new KeyInputBinding(Key.Q, Ctrl)),
        new(KeybindingActions.EditUndo, "Edit", "Undo", new KeyInputBinding(Key.Z, Ctrl)),
        new(KeybindingActions.EditRedo, "Edit", "Redo", new KeyInputBinding(Key.Z, CtrlShift)),
        new(KeybindingActions.ToolSelect, "Tools", "Select Tool", null),
        new(KeybindingActions.ToolEdit, "Tools", "Edit Tool", null),
        new(KeybindingActions.ToolCreate, "Tools", "Create Tool", null),
        new(KeybindingActions.ToolTexture, "Tools", "Texture Tool", null),
        new(KeybindingActions.Confirm, "Editing", "Confirm", new KeyInputBinding(Key.Enter)),
        new(KeybindingActions.Cancel, "Editing", "Cancel", new KeyInputBinding(Key.Escape)),
        new(
            KeybindingActions.DeleteSelection,
            "Editing",
            "Delete Selection",
            new KeyInputBinding(Key.Delete)
        ),
        new(KeybindingActions.CameraForward, "Camera", "Move Forward", new KeyInputBinding(Key.W)),
        new(KeybindingActions.CameraBack, "Camera", "Move Back", new KeyInputBinding(Key.S)),
        new(KeybindingActions.CameraLeft, "Camera", "Move Left", new KeyInputBinding(Key.A)),
        new(KeybindingActions.CameraRight, "Camera", "Move Right", new KeyInputBinding(Key.D)),
        new(
            KeybindingActions.CameraLook,
            "Camera",
            "Camera Look",
            new MouseInputBinding(MouseButton.Right)
        ),
    ];

    private static readonly Dictionary<string, KeybindingDefinition> ByActionId = BuildLookup();

    public static IReadOnlyList<KeybindingDefinition> All => Definitions;

    public static KeybindingDefinition Get(string actionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        return ByActionId.TryGetValue(actionId, out KeybindingDefinition? definition)
            ? definition
            : throw new ArgumentException($"Unknown input action '{actionId}'.", nameof(actionId));
    }

    public static bool TryGet(string actionId, out KeybindingDefinition? definition) =>
        ByActionId.TryGetValue(actionId, out definition);

    private static Dictionary<string, KeybindingDefinition> BuildLookup()
    {
        Dictionary<string, KeybindingDefinition> lookup = new(StringComparer.Ordinal);
        foreach (KeybindingDefinition definition in Definitions)
        {
            if (!lookup.TryAdd(definition.ActionId, definition))
                throw new InvalidOperationException(
                    $"Duplicate keybinding action '{definition.ActionId}'."
                );
        }

        return lookup;
    }
}
