using System;
using Godot;

// Shared selection and hover behavior for editor tools.
public abstract class ToolBase : IEditorTool
{
    private readonly SelectionModeState? _selectionMode;

    protected ToolBase(SelectionModeState? selectionMode = null)
    {
        _selectionMode = selectionMode;
    }

    public abstract string Id { get; }

    public void OnMouseDown(IEditorToolContext context, EditorMouseInput input)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (input.Camera is null)
            return;

        OnPreMouseDown(context, input);

        if (_selectionMode?.Mode == EditorMode.Component)
        {
            HandleComponentSelection(context, input);
            return;
        }

        if (TryBeginInteraction(context, input))
            return;

        HandleObjectSelection(context, input);
    }

    public abstract void OnMouseDrag(IEditorToolContext context, EditorMouseInput input);

    public void OnMouseMove(IEditorToolContext context, EditorMouseInput input)
    {
        ArgumentNullException.ThrowIfNull(context);
        UpdateHover(context, input);
        OnToolMouseMove(context, input);
    }

    public abstract void OnMouseUp(IEditorToolContext context, EditorMouseInput input);

    public void Cancel(IEditorToolContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Selection.ClearHover();
        OnCancel(context);
    }

    // Called before selection/interaction classification. Subclasses cancel any in-progress
    // interaction here so a new press starts from a clean slate.
    protected virtual void OnPreMouseDown(IEditorToolContext context, EditorMouseInput input) { }

    // Tool-specific press handling (for example a gizmo grab). Return true when the press was
    // consumed so selection is skipped.
    protected virtual bool TryBeginInteraction(IEditorToolContext context, EditorMouseInput input) =>
        false;

    protected virtual void OnToolMouseMove(IEditorToolContext context, EditorMouseInput input) { }

    protected virtual void OnCancel(IEditorToolContext context) { }

    private void HandleComponentSelection(IEditorToolContext context, EditorMouseInput input)
    {
        ComponentPickingHit? componentHit = context.Picking.PickComponent(
            input.Camera!,
            input.ScreenPosition,
            _selectionMode!.Components
        );
        context.Selection.SetHoveredComponent(componentHit?.Component);
        SelectionValue beforeComponentSelection = context.Selection.Current;
        SelectionValue afterComponentSelection = input.IsShiftPressed
            ? componentHit is ComponentPickingHit toggledHit
                ? beforeComponentSelection.Toggle(toggledHit.Component)
                : beforeComponentSelection
            : componentHit is ComponentPickingHit hit
                ? SelectionValue.ForComponent(hit.Component)
                : SelectionValue.Empty;
        if (beforeComponentSelection != afterComponentSelection)
        {
            context.Commands.Submit(
                new SelectionCommand(
                    context.Selection,
                    beforeComponentSelection,
                    afterComponentSelection
                )
            );
        }
    }

    private static void HandleObjectSelection(IEditorToolContext context, EditorMouseInput input)
    {
        SelectionValue beforeSelection = context.Selection.Current;
        MeshObjectId? picked = context.Picking.Raycast(input.Camera!, input.ScreenPosition);
        SelectionValue afterSelection = input.IsShiftPressed
            ? picked is MeshObjectId toggledId
                ? beforeSelection.Toggle(toggledId)
                : beforeSelection
            : picked is MeshObjectId pickedId
                ? SelectionValue.ForObject(pickedId)
                : SelectionValue.Empty;
        if (afterSelection != beforeSelection)
        {
            context.Commands.Submit(
                new SelectionCommand(context.Selection, beforeSelection, afterSelection)
            );
        }
    }

    private void UpdateHover(IEditorToolContext context, EditorMouseInput input)
    {
        if (input.Camera is null || _selectionMode?.Mode != EditorMode.Component)
        {
            context.Selection.ClearHover();
            return;
        }

        ComponentPickingHit? hit = context.Picking.PickComponent(
            input.Camera,
            input.ScreenPosition,
            _selectionMode.Components
        );
        context.Selection.SetHoveredComponent(hit?.Component);
    }
}
