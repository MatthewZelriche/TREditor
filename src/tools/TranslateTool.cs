using System;
using System.Collections.Generic;
using Godot;
using NumericsVector3 = System.Numerics.Vector3;

// Moves selected objects or mesh components with the viewport gizmos. The tool owns the three-phase
// flow only: the coordinator owns the gizmo nodes, previews carry drag feedback, and the document
// changes once on mouse up through one command.
public sealed class TranslateTool : ToolBase
{
    public const string ToolId = "tool.translate";

    private bool _dragging;
    private MeshObject[]? _before;
    private ComponentDrag[]? _componentDrags;
    private Transform3D _gizmoStart;
    private Camera3D? _sourceCamera;

    public TranslateTool(SelectionModeState? selectionMode = null)
        : base(selectionMode)
    { }

    public override string Id => ToolId;

    // Classifying the press as a gizmo grab before selection keeps a selection click from starting
    // a translation of the object it just selected.
    protected override void OnPreMouseDown(IEditorToolContext context, EditorMouseInput input)
    {
        if (_dragging)
            Cancel(context);
    }

    protected override bool TryBeginInteraction(IEditorToolContext context, EditorMouseInput input)
    {
        if (input.IsShiftPressed)
            return false;

        SelectionValue beforeSelection = context.Selection.Current;
        if (SelectionMode?.Mode == EditorMode.Component)
            return TryBeginComponentInteraction(context, input, beforeSelection.Components);

        if (!TryCaptureSelectedObjects(context, beforeSelection.ObjectIds, out MeshObject[] selected))
            return false;
        if (!context.TranslateGizmos.BeginInteraction(input.Camera!, input.ScreenPosition))
            return false;

        _dragging = true;
        _before = selected;
        _gizmoStart = context.TranslateGizmos.ProxyTransform;
        _sourceCamera = input.Camera;
        return true;
    }

    public override void OnMouseDrag(IEditorToolContext context, EditorMouseInput input)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!_dragging || !ReferenceEquals(input.Camera, _sourceCamera))
            return;

        if (!context.TranslateGizmos.UpdateInteraction(input.Camera, input.ScreenPosition))
            return;

        Vector3 delta = context.TranslateGizmos.ProxyTransform.Origin - _gizmoStart.Origin;
        if (_componentDrags is not null)
        {
            if (!UpdateComponentPreviews(context, delta))
                CleanUp(context);
            return;
        }

        // The proxy is the only thing the gizmo moved. Apply its translation delta to every selected
        // object as preview state while leaving the document untouched until mouse up.
        foreach (MeshObject before in _before!)
        {
            context.Preview.SetTransform(
                before.Id,
                new Transform3D(before.Transform.Basis, before.Transform.Origin + delta)
            );
        }
    }

    public override void OnMouseUp(IEditorToolContext context, EditorMouseInput input)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!_dragging || !ReferenceEquals(input.Camera, _sourceCamera))
            return;

        try
        {
            context.TranslateGizmos.EndInteraction();
            Vector3 delta = context.TranslateGizmos.ProxyTransform.Origin - _gizmoStart.Origin;
            if (delta.IsZeroApprox())
                return;

            if (_componentDrags is not null)
                CommitComponents(context);
            else
                CommitObjects(context, delta);
        }
        finally
        {
            // A command that throws must not leave the preview, the proxy, or this tool mid-drag.
            CleanUp(context);
        }
    }

    // The one place a translate interaction is torn down. Lifecycle transitions cancel through the
    // registry rather than reaching for the coordinator or previews themselves.
    protected override void OnCancel(IEditorToolContext context)
    {
        if (!_dragging)
            return;

        CleanUp(context);
    }

    private void CleanUp(IEditorToolContext context)
    {
        MeshObject[]? before = _before;
        ComponentDrag[]? componentDrags = _componentDrags;
        _dragging = false;
        _before = null;
        _componentDrags = null;
        _sourceCamera = null;

        try
        {
            if (before is not null)
            {
                foreach (MeshObject meshObject in before)
                    context.Preview.ClearTransform(meshObject.Id);
            }

            if (componentDrags is not null)
            {
                foreach (ComponentDrag drag in componentDrags)
                    context.Preview.ClearMesh(drag.Selection.Object.Id);
            }
        }
        finally
        {
            // Restores the proxy from whatever the document and previews now say, which is the
            // committed transform after a successful move and the original one after a cancel.
            context.TranslateGizmos.CancelAndSynchronize();
        }
    }

    private bool TryBeginComponentInteraction(
        IEditorToolContext context,
        EditorMouseInput input,
        IReadOnlySet<MeshComponentId> components
    )
    {
        if (
            !ComponentVertexSelectionResolver.TryResolve(
                context.Document,
                components,
                SelectionMode!.Components,
                previews: null,
                out ComponentVertexSelection[] selections
            )
            || !context.TranslateGizmos.BeginInteraction(input.Camera!, input.ScreenPosition)
        )
        {
            return false;
        }

        var drags = new ComponentDrag[selections.Length];
        int created = 0;
        try
        {
            for (; created < selections.Length; created++)
            {
                ComponentVertexSelection selection = selections[created];
                // TODO: full mesh copy until a delta/COW mesh API exists.
                GDMesh preview = new() { MeshData = selection.Object.Mesh.MeshData };
                drags[created] = new ComponentDrag(selection, preview);
                context.Preview.SetMesh(selection.Object.Id, preview);
            }
        }
        catch
        {
            for (int index = 0; index < created; index++)
                context.Preview.ClearMesh(drags[index].Selection.Object.Id);
            context.TranslateGizmos.CancelAndSynchronize();
            throw;
        }

        _dragging = true;
        _componentDrags = drags;
        _gizmoStart = context.TranslateGizmos.ProxyTransform;
        _sourceCamera = input.Camera;
        return true;
    }

    private bool UpdateComponentPreviews(IEditorToolContext context, Vector3 worldDelta)
    {
        foreach (ComponentDrag drag in _componentDrags!)
        {
            if (
                !context.Preview.TryGetMesh(drag.Selection.Object.Id, out GDMesh preview)
                || !ReferenceEquals(preview, drag.Preview)
            )
            {
                return false;
            }

            preview.SetVertexPositions(
                drag.Selection.Vertices,
                TranslatedPositions(drag.Selection, worldDelta)
            );
            context.Preview.RefreshMesh(drag.Selection.Object.Id);
        }

        return true;
    }

    private void CommitComponents(IEditorToolContext context)
    {
        ComponentDrag[] drags = _componentDrags!;
        var before = new MeshObject[drags.Length];
        var replacementMeshes = new GDMesh[drags.Length];
        int taken = 0;
        try
        {
            for (; taken < drags.Length; taken++)
            {
                ComponentVertexSelection selection = drags[taken].Selection;
                if (!context.Preview.TryTakeMesh(selection.Object.Id, out GDMesh mesh))
                    throw new InvalidOperationException("A component preview mesh is unavailable.");

                before[taken] = selection.Object;
                replacementMeshes[taken] = mesh;
            }
        }
        catch
        {
            for (int index = 0; index < taken; index++)
                replacementMeshes[index].Dispose();
            throw;
        }

        var command = new TranslateComponentsCommand(before, replacementMeshes);
        try
        {
            context.Commands.Submit(command);
            if (!command.IsApplied)
                command.Dispose();
        }
        catch
        {
            command.Dispose();
            throw;
        }
    }

    private void CommitObjects(IEditorToolContext context, Vector3 delta)
    {
        MeshObject[] before = _before!;
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

    private static NumericsVector3[] TranslatedPositions(
        ComponentVertexSelection selection,
        Vector3 worldDelta
    )
    {
        Vector3 localDelta = selection.Object.Transform.Basis.Inverse() * worldDelta;
        var positions = new NumericsVector3[selection.Positions.Length];
        var numericDelta = new NumericsVector3(localDelta.X, localDelta.Y, localDelta.Z);
        for (int index = 0; index < positions.Length; index++)
            positions[index] = selection.Positions[index] + numericDelta;
        return positions;
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

    private sealed record ComponentDrag(ComponentVertexSelection Selection, GDMesh Preview);
}
