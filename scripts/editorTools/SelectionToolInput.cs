using System;
using Godot;

public static class SelectionToolInput
{
    public static EditorToolResult HandleMouseButton(
        EditorToolContext context,
        ViewportMouseButtonEvent input,
        ScenePickElementFilter filter,
        bool xRayMode = false
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        if (input.Button != MouseButton.Left || !input.Pressed)
        {
            return EditorToolResult.Continue;
        }

        SelectionSnapshot before = context.Selection.Current;
        SelectionTarget target = default;
        bool pickSucceeded =
            context.ScenePicking.TryPickScene(
                input.RayOrigin,
                input.RayDirection,
                out ScenePickHit hit,
                filter,
                xRayMode
            ) && SelectionTarget.TryFromHit(hit, out target);

        SelectionSnapshot? after = ResolveSelectionAfterPick(
            before,
            pickSucceeded,
            target,
            input.Modifiers
        );
        if (after == null)
        {
            return EditorToolResult.Continue;
        }

        EditorCommand command = SetSelectionCommand.CreateIfChanged(before, after.Value);
        return command == null
            ? EditorToolResult.Continue
            : EditorToolResult.ContinueWithCommand(command);
    }

    /// <summary>
    /// Computes the post-click selection state. Returns <see langword="null"/> when a miss with
    /// shift or ctrl held should leave the current selection unchanged.
    /// </summary>
    public static SelectionSnapshot? ResolveSelectionAfterPick(
        SelectionSnapshot before,
        bool pickSucceeded,
        SelectionTarget target,
        ViewportInputModifiers modifiers
    )
    {
        if (!pickSucceeded)
        {
            if (modifiers.ShiftPressed || modifiers.CtrlPressed)
            {
                return null;
            }

            return SelectionSnapshot.Empty;
        }

        return before.Apply(GetChangeMode(modifiers), target);
    }

    private static SelectionChangeMode GetChangeMode(ViewportInputModifiers modifiers)
    {
        if (modifiers.CtrlPressed)
        {
            return SelectionChangeMode.Toggle;
        }

        if (modifiers.ShiftPressed)
        {
            return SelectionChangeMode.Add;
        }

        return SelectionChangeMode.Replace;
    }
}
