using System;
using System.Collections.Generic;
using Godot;

// Shared selection and hover behavior for editor tools.
public abstract class ToolBase : IEditorTool
{
    protected ToolBase(SelectionModeState? selectionMode = null)
    {
        SelectionMode = selectionMode;
    }

    protected SelectionModeState? SelectionMode { get; }

    public abstract string Id { get; }

    public void OnMouseDown(IEditorToolContext context, EditorMouseInput input)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (input.Camera is null)
            return;

        OnPreMouseDown(context, input);

        if (TryBeginInteraction(context, input))
            return;

        if (SelectionMode?.Mode == EditorMode.Component)
            HandleComponentSelection(context, input);
        else
            HandleObjectSelection(context, input);
    }

    public virtual void OnMouseDrag(IEditorToolContext context, EditorMouseInput input) { }

    public void OnMouseMove(IEditorToolContext context, EditorMouseInput input)
    {
        ArgumentNullException.ThrowIfNull(context);
        UpdateHover(context, input);
        OnToolMouseMove(context, input);
    }

    public virtual void OnMouseUp(IEditorToolContext context, EditorMouseInput input) { }

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
            SelectionMode!.Components,
            xRay: SelectionMode.XRayEnabled
        );
        context.Selection.SetHoveredComponent(componentHit?.Component);
        SelectionValue beforeComponentSelection = context.Selection.Current;
        SelectionValue afterComponentSelection = componentHit is ComponentPickingHit loopHit
            && input.IsDoubleClick
            && loopHit.Component.Type == MeshComponentType.Edge
            && context.Document.TryGetObject(loopHit.Component.ObjectId, out MeshObject meshObject)
                ? SelectEdgeLoop(beforeComponentSelection, loopHit.Component, meshObject, input.IsShiftPressed)
            : input.IsShiftPressed
            ? componentHit is ComponentPickingHit toggledHit
                ? beforeComponentSelection.Toggle(toggledHit.Component)
                : beforeComponentSelection
            : componentHit is ComponentPickingHit hit
                ? SelectionValue.ForComponent(hit.Component)
                : SelectionValue.Empty;
        if (beforeComponentSelection != afterComponentSelection)
        {
            context.Commands.Submit(context.Selection.CreateCommand(afterComponentSelection));
        }
    }

    private static SelectionValue SelectEdgeLoop(
        SelectionValue before,
        MeshComponentId seed,
        MeshObject meshObject,
        bool preserveSelection
    )
    {
        IReadOnlyList<int> edgeSlots = EdgeLoopSelection.Collect(
            meshObject.Mesh.SpatialMesh,
            seed.Slot
        );
        var components = preserveSelection
            ? new List<MeshComponentId>(before.OrderedComponents)
            : [];
        foreach (int slot in edgeSlots)
            components.Add(new MeshComponentId(seed.ObjectId, MeshComponentType.Edge, slot));

        return SelectionValue.ForSelections(
            preserveSelection ? before.ObjectIds : [],
            components
        );
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
            context.Commands.Submit(context.Selection.CreateCommand(afterSelection));
    }

    private void UpdateHover(IEditorToolContext context, EditorMouseInput input)
    {
        if (input.Camera is null || SelectionMode?.Mode != EditorMode.Component)
        {
            context.Selection.ClearHover();
            return;
        }

        ComponentPickingHit? hit = context.Picking.PickComponent(
            input.Camera,
            input.ScreenPosition,
            SelectionMode.Components,
            xRay: SelectionMode.XRayEnabled
        );
        context.Selection.SetHoveredComponent(hit?.Component);
    }
}
