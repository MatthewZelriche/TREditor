using System;
using System.Collections.Generic;
using Godot;

// Moves the selected objects with the viewport gizmos. The tool owns the three-phase flow only: the
// coordinator owns the gizmo nodes, previews carry the drag feedback, and the document changes once,
// on mouse up, through a single command that replaces the MeshObject records captured at drag start.
public sealed class TranslateTool : IEditorTool
{
    public const string ToolId = "tool.translate";

    private bool _dragging;
    private MeshObject[]? _before;
    private Transform3D _gizmoStart;
    private Camera3D? _sourceCamera;
    private readonly SelectionModeState? _selectionMode;

    public TranslateTool(SelectionModeState? selectionMode = null)
    {
        _selectionMode = selectionMode;
    }

    public string Id => ToolId;

    // A press is either a gizmo grab or a selection click, never both. Classifying it before any
    // state changes keeps a selection click from starting a translation of the object it just
    // selected.
    // TODO: Base Tool class that handles selection for all tools
    public void OnMouseDown(IEditorToolContext context, EditorMouseInput input)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (input.Camera is null)
            return;

        if (_dragging)
            Cancel(context);

        if (_selectionMode?.Mode == EditorMode.Component)
        {
            ComponentPickingHit? componentHit = context.Picking.PickComponent(
                input.Camera,
                input.ScreenPosition,
                _selectionMode.Components
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

            return;
        }

        SelectionValue beforeSelection = context.Selection.Current;
        if (
            !input.IsShiftPressed
            &&
            TryCaptureSelectedObjects(context, beforeSelection.ObjectIds, out MeshObject[] selected)
            && context.TranslateGizmos.BeginInteraction(input.Camera, input.ScreenPosition)
        )
        {
            _dragging = true;
            _before = selected;
            _gizmoStart = context.TranslateGizmos.ProxyTransform;
            _sourceCamera = input.Camera;
            return;
        }

        MeshObjectId? picked = context.Picking.Raycast(input.Camera, input.ScreenPosition);
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

    public void OnMouseDrag(IEditorToolContext context, EditorMouseInput input)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!_dragging || _before is null || !ReferenceEquals(input.Camera, _sourceCamera))
            return;

        if (!context.TranslateGizmos.UpdateInteraction(input.Camera, input.ScreenPosition))
            return;

        // The proxy is the only thing the gizmo moved. Apply its translation delta to every selected
        // object as preview state while leaving the document untouched until mouse up.
        Vector3 delta = context.TranslateGizmos.ProxyTransform.Origin - _gizmoStart.Origin;
        foreach (MeshObject before in _before)
        {
            context.Preview.SetTransform(
                before.Id,
                new Transform3D(before.Transform.Basis, before.Transform.Origin + delta)
            );
        }
    }

    public void OnMouseMove(IEditorToolContext context, EditorMouseInput input)
    {
        ArgumentNullException.ThrowIfNull(context);
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

    public void OnMouseUp(IEditorToolContext context, EditorMouseInput input)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!_dragging || _before is null || !ReferenceEquals(input.Camera, _sourceCamera))
            return;

        MeshObject[] before = _before;
        try
        {
            context.TranslateGizmos.EndInteraction();
            Vector3 delta = context.TranslateGizmos.ProxyTransform.Origin - _gizmoStart.Origin;
            if (!delta.IsZeroApprox())
            {
                var after = new MeshObject[before.Length];
                for (int index = 0; index < before.Length; index++)
                {
                    MeshObject meshObject = before[index];
                    after[index] = meshObject with
                    {
                        Transform = new Transform3D(
                            meshObject.Transform.Basis,
                            meshObject.Transform.Origin + delta
                        ),
                    };
                }

                context.Commands.Submit(new TranslateCommand(before, after));
            }
        }
        finally
        {
            // A command that throws must not leave the preview, the proxy, or this tool mid-drag.
            CleanUp(context);
        }
    }

    // The one place a translate interaction is torn down. Lifecycle transitions cancel through the
    // registry rather than reaching for the coordinator or previews themselves.
    public void Cancel(IEditorToolContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Selection.ClearHover();
        if (!_dragging || _before is null)
            return;

        CleanUp(context);
    }

    private void CleanUp(IEditorToolContext context)
    {
        MeshObject[] before = _before!;
        _dragging = false;
        _before = null;
        _sourceCamera = null;

        try
        {
            foreach (MeshObject meshObject in before)
                context.Preview.ClearTransform(meshObject.Id);
        }
        finally
        {
            // Restores the proxy from whatever the document and previews now say, which is the
            // committed transform after a successful move and the original one after a cancel.
            context.TranslateGizmos.CancelAndSynchronize();
        }
    }

    private static bool TryCaptureSelectedObjects(
        IEditorToolContext context,
        IReadOnlySet<MeshObjectId> selectedIds,
        out MeshObject[] selected
    )
    {
        if (selectedIds.Count == 0)
        {
            selected = [];
            return false;
        }

        selected = new MeshObject[selectedIds.Count];
        int index = 0;
        foreach (MeshObjectId id in selectedIds)
        {
            if (!context.Document.TryGetObject(id, out MeshObject meshObject))
            {
                selected = [];
                return false;
            }

            selected[index++] = meshObject;
        }

        return true;
    }
}
