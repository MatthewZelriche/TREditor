using System;
using Godot;

public static class SelectionToolInput
{
    public static EditorToolResult HandleMouseButton(
        EditorToolContext context,
        ViewportMouseButtonEvent input,
        ScenePickElementFilter filter
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        if (input.Button != MouseButton.Left || !input.Pressed)
        {
            return EditorToolResult.Continue;
        }

        SelectionSnapshot before = context.Selection.Current;
        SelectionSnapshot after;

        if (
            !context.ScenePicking.TryPickScene(
                input.RayOrigin,
                input.RayDirection,
                out ScenePickHit hit,
                filter
            ) || !SelectionTarget.TryFromHit(hit, out SelectionTarget target)
        )
        {
            if (input.Modifiers.ShiftPressed || input.Modifiers.CtrlPressed)
            {
                return EditorToolResult.Continue;
            }

            after = SelectionSnapshot.Empty;
        }
        else
        {
            after = before.Apply(GetChangeMode(input.Modifiers), target);
        }

        EditorCommand command = SetSelectionCommand.CreateIfChanged(before, after);
        return command == null
            ? EditorToolResult.Continue
            : EditorToolResult.ContinueWithCommand(command);
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
