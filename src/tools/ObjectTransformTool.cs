using System;
using System.Collections.Generic;
using Godot;

// Shared object transform interaction. Gizmo3D mutates the coordinator's proxy; this tool
// projects that proxy delta onto the selected objects as previews and commits once on mouse up.
public abstract class ObjectTransformTool : ToolBase
{
    private readonly TransformOperation _operation;
    private bool _dragging;
    private MeshObject[]? _before;
    private Transform3D _gizmoStart;
    private Camera3D? _sourceCamera;

    protected ObjectTransformTool(
        TransformOperation operation,
        SelectionModeState? selectionMode = null
    )
        : base(selectionMode)
    {
        _operation = operation;
    }

    // Rotate/scale are object-only; translate also runs this path outside component mode.
    protected virtual bool CanBeginObjectTransform =>
        SelectionMode?.Mode == EditorMode.Object;

    protected Transform3D GizmoStart => _gizmoStart;

    protected Camera3D? SourceCamera => _sourceCamera;

    protected override void OnPreMouseDown(IEditorToolContext context, EditorMouseInput input)
    {
        if (_dragging)
            Cancel(context);
    }

    protected override bool TryBeginInteraction(
        IEditorToolContext context,
        EditorMouseInput input
    )
    {
        if (
            input.IsShiftPressed
            || !CanBeginObjectTransform
            || !TryCaptureSelectedObjects(
                context,
                context.Selection.Current.ObjectIds,
                out MeshObject[] selected
            )
            || !context.TranslateGizmos.BeginInteraction(input.Camera!, input.ScreenPosition)
        )
        {
            return false;
        }

        BeginDrag(selected, input.Camera, context.TranslateGizmos.ProxyTransform);
        return true;
    }

    public override void OnMouseDrag(IEditorToolContext context, EditorMouseInput input)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (
            !_dragging
            || !ReferenceEquals(input.Camera, _sourceCamera)
            || !context.TranslateGizmos.UpdateInteraction(input.Camera, input.ScreenPosition)
        )
        {
            return;
        }

        Preview(context, DeltaFromStart(context.TranslateGizmos.ProxyTransform));
    }

    public override void OnMouseUp(IEditorToolContext context, EditorMouseInput input)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!_dragging || !ReferenceEquals(input.Camera, _sourceCamera))
            return;

        try
        {
            context.TranslateGizmos.EndInteraction();
            Transform3D current = context.TranslateGizmos.ProxyTransform;
            if (current.IsEqualApprox(_gizmoStart))
                return;

            Transform3D delta = DeltaFromStart(current);
            MeshObject[] before = _before!;
            var after = new MeshObject[before.Length];
            for (int index = 0; index < before.Length; index++)
                after[index] = before[index] with { Transform = delta * before[index].Transform };

            context.Commands.Submit(new TransformCommand(_operation, before, after));
        }
        finally
        {
            CleanUpObjectDrag(context);
        }
    }

    protected override void OnCancel(IEditorToolContext context)
    {
        if (_dragging)
            CleanUpObjectDrag(context);
    }

    protected void BeginDrag(
        MeshObject[]? before,
        Camera3D? camera,
        Transform3D gizmoStart
    )
    {
        _dragging = true;
        _before = before;
        _gizmoStart = gizmoStart;
        _sourceCamera = camera;
    }

    protected void CleanUpObjectDrag(IEditorToolContext context)
    {
        MeshObject[]? before = _before;
        _dragging = false;
        _before = null;
        _sourceCamera = null;

        try
        {
            if (before is not null)
            {
                foreach (MeshObject meshObject in before)
                    context.Preview.ClearTransform(meshObject.Id);
            }
        }
        finally
        {
            context.TranslateGizmos.CancelAndSynchronize();
        }
    }

    private Transform3D DeltaFromStart(Transform3D current) =>
        current * _gizmoStart.AffineInverse();

    private void Preview(IEditorToolContext context, Transform3D delta)
    {
        foreach (MeshObject before in _before!)
            context.Preview.SetTransform(before.Id, delta * before.Transform);
    }

    private static bool TryCaptureSelectedObjects(
        IEditorToolContext context,
        IReadOnlySet<MeshObjectId> selectedIds,
        out MeshObject[] selected
    )
    {
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

        return selected.Length > 0;
    }
}
