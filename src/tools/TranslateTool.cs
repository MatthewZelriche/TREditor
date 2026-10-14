using System;
using System.Collections.Generic;
using Godot;
using TRMesh.Mesh;
using NumericsVector3 = System.Numerics.Vector3;

// Moves selected objects or mesh components with the viewport gizmos. Object drags use the shared
// ObjectTransformTool path; this type owns the component-translation flow.
public sealed class TranslateTool : ObjectTransformTool
{
    public const string ToolId = "tool.translate";

    private ComponentDrag[]? _componentDrags;
    private ExtrusionDrag? _extrusionDrag;

    public TranslateTool(SelectionModeState? selectionMode = null)
        : base(TransformOperation.Translate, selectionMode)
    { }

    public override string Id => ToolId;

    protected override bool CanBeginObjectTransform =>
        SelectionMode?.Mode != EditorMode.Component;

    protected override void OnPreMouseDown(IEditorToolContext context, EditorMouseInput input)
    {
        if (_componentDrags is not null || _extrusionDrag is not null)
            Cancel(context);
        else
            base.OnPreMouseDown(context, input);
    }

    protected override bool TryBeginInteraction(IEditorToolContext context, EditorMouseInput input)
    {
        if (SelectionMode?.Mode == EditorMode.Component)
        {
            if (input.IsShiftPressed)
                return TryBeginExtrusion(context, input);
            return TryBeginComponentInteraction(context, input, context.Selection.Current.Components);
        }

        if (input.IsShiftPressed)
            return TryBeginDuplicate(context, input);

        return base.TryBeginInteraction(context, input);
    }

    // Shift-translate in object mode duplicates the selection, then drags the copies so the
    // originals stay put. The duplicate is recorded immediately; the following translate is a
    // separate undo step when the drag moves.
    private bool TryBeginDuplicate(IEditorToolContext context, EditorMouseInput input)
    {
        if (
            !CanBeginObjectTransform
            || !TryCaptureSelectedObjects(
                context,
                context.Selection.Current.ObjectIds,
                out MeshObject[] sources
            )
            || !context.TranslateGizmos.BeginInteraction(input.Camera!, input.ScreenPosition)
        )
        {
            return false;
        }

        var command = new DuplicateObjectsCommand(sources, context.Selection);
        try
        {
            if (!context.Commands.TrySubmit(command, out _))
            {
                command.Dispose();
                context.TranslateGizmos.CancelAndSynchronize();
                return false;
            }
        }
        catch
        {
            command.Dispose();
            context.TranslateGizmos.CancelAndSynchronize();
            throw;
        }

        if (
            !TryCaptureSelectedObjects(
                context,
                context.Selection.Current.ObjectIds,
                out MeshObject[] duplicates
            )
        )
        {
            // Duplicate already committed; consume the press so selection is left alone.
            context.TranslateGizmos.CancelAndSynchronize();
            return true;
        }

        BeginDrag(duplicates, input.Camera, context.TranslateGizmos.ProxyTransform);
        return true;
    }

    public override void OnMouseDrag(IEditorToolContext context, EditorMouseInput input)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_extrusionDrag is not null)
        {
            UpdateExtrusionDrag(context, input);
            return;
        }
        if (_componentDrags is null)
        {
            base.OnMouseDrag(context, input);
            return;
        }

        if (
            !ReferenceEquals(input.Camera, SourceCamera)
            || !context.TranslateGizmos.UpdateInteraction(input.Camera!, input.ScreenPosition)
        )
        {
            return;
        }

        Vector3 delta = context.TranslateGizmos.ProxyTransform.Origin - GizmoStart.Origin;
        if (!UpdateComponentPreviews(context, delta))
            CleanUpComponents(context);
    }

    public override void OnMouseUp(IEditorToolContext context, EditorMouseInput input)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_extrusionDrag is not null)
        {
            FinishExtrusionDrag(context, input);
            return;
        }
        if (_componentDrags is null)
        {
            base.OnMouseUp(context, input);
            return;
        }

        if (!ReferenceEquals(input.Camera, SourceCamera))
            return;

        try
        {
            context.TranslateGizmos.EndInteraction();
            Vector3 delta = context.TranslateGizmos.ProxyTransform.Origin - GizmoStart.Origin;
            if (!delta.IsZeroApprox())
                CommitComponents(context, delta);
        }
        finally
        {
            CleanUpComponents(context);
        }
    }

    protected override void OnCancel(IEditorToolContext context)
    {
        if (_extrusionDrag is not null)
            CleanUpExtrusion(context);
        else if (_componentDrags is not null)
            CleanUpComponents(context);
        else
            base.OnCancel(context);
    }

    private void CleanUpComponents(IEditorToolContext context)
    {
        ComponentDrag[]? componentDrags = _componentDrags;
        _componentDrags = null;

        try
        {
            if (componentDrags is not null)
            {
                foreach (ComponentDrag drag in componentDrags)
                    context.Preview.ClearMesh(drag.Selection.Object.Id);
            }
        }
        finally
        {
            CleanUpObjectDrag(context);
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
                // The preview is isolated from the authoritative mesh; only the final vertex write
                // is recorded as a sparse patch by TranslateComponentsCommand.
                GDMesh preview = new(selection.Object.Mesh.SpatialMesh.Clone());
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

        _componentDrags = drags;
        BeginDrag(before: null, input.Camera, context.TranslateGizmos.ProxyTransform);
        return true;
    }

    private bool TryBeginExtrusion(IEditorToolContext context, EditorMouseInput input)
    {
        FaceExtrusionPlan plan;
        try
        {
            plan = ExtrudeFacesCommand.ResolvePlan(
                context.Document,
                context.Selection.Current
            );
        }
        catch (ArgumentException)
        {
            return false;
        }

        if (!context.TranslateGizmos.BeginInteraction(input.Camera!, input.ScreenPosition))
            return false;

        GDMesh? preview = null;
        bool transferred = false;
        try
        {
            preview = new GDMesh(plan.Object.Mesh.SpatialMesh.Clone());
            FaceExtrusion extrusion = preview.SpatialMesh.ExtrudeFaces(
                plan.Faces,
                NumericsVector3.Zero
            );
            var positions = new NumericsVector3[extrusion.Vertices.Length];
            for (int index = 0; index < positions.Length; index++)
            {
                positions[index] = preview.SpatialMesh.GetVertexData(
                    extrusion.Vertices[index]
                ).position;
            }

            context.Preview.SetMesh(plan.Object.Id, preview);
            transferred = true;
            _extrusionDrag = new ExtrusionDrag(plan, preview, extrusion.Vertices, positions);
            BeginDrag(before: null, input.Camera, context.TranslateGizmos.ProxyTransform);
            return true;
        }
        catch
        {
            if (!transferred)
                preview?.Dispose();
            context.TranslateGizmos.CancelAndSynchronize();
            throw;
        }
    }

    private void UpdateExtrusionDrag(IEditorToolContext context, EditorMouseInput input)
    {
        if (
            !ReferenceEquals(input.Camera, SourceCamera)
            || !context.TranslateGizmos.UpdateInteraction(input.Camera!, input.ScreenPosition)
        )
        {
            return;
        }

        ExtrusionDrag drag = _extrusionDrag!;
        if (
            !context.Preview.TryGetMesh(drag.Plan.Object.Id, out GDMesh preview)
            || !ReferenceEquals(preview, drag.Preview)
        )
        {
            CleanUpExtrusion(context);
            return;
        }

        Vector3 worldDelta = context.TranslateGizmos.ProxyTransform.Origin - GizmoStart.Origin;
        preview.SetVertexPositions(
            drag.CapVertices,
            OffsetPositions(drag.StartPositions, drag.Plan.Object.Transform, worldDelta)
        );
        context.Preview.RefreshMesh(drag.Plan.Object.Id);
    }

    private void FinishExtrusionDrag(IEditorToolContext context, EditorMouseInput input)
    {
        if (!ReferenceEquals(input.Camera, SourceCamera))
            return;

        try
        {
            context.TranslateGizmos.EndInteraction();
            Vector3 worldDelta =
                context.TranslateGizmos.ProxyTransform.Origin - GizmoStart.Origin;
            if (!worldDelta.IsZeroApprox())
            {
                context.Commands.Submit(
                    ExtrudeFacesCommand.Create(
                        _extrusionDrag!.Plan,
                        worldDelta,
                        context.Selection
                    )
                );
            }
        }
        finally
        {
            CleanUpExtrusion(context);
        }
    }

    private void CleanUpExtrusion(IEditorToolContext context)
    {
        ExtrusionDrag? drag = _extrusionDrag;
        _extrusionDrag = null;
        try
        {
            if (drag is not null)
                context.Preview.ClearMesh(drag.Plan.Object.Id);
        }
        finally
        {
            CleanUpObjectDrag(context);
        }
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

    private void CommitComponents(IEditorToolContext context, Vector3 worldDelta)
    {
        ComponentDrag[] drags = _componentDrags!;
        var edits = new ComponentVertexSelection[drags.Length];
        for (int index = 0; index < drags.Length; index++)
        {
            ComponentVertexSelection selection = drags[index].Selection;
            if (
                !context.Preview.TryGetMesh(selection.Object.Id, out GDMesh preview)
                || !ReferenceEquals(preview, drags[index].Preview)
            )
            {
                throw new InvalidOperationException("A component preview mesh is unavailable.");
            }

            edits[index] = selection with { Positions = TranslatedPositions(selection, worldDelta) };
        }

        context.Commands.Submit(new TranslateComponentsCommand(edits));
    }

    private static NumericsVector3[] TranslatedPositions(
        ComponentVertexSelection selection,
        Vector3 worldDelta
    ) => OffsetPositions(selection.Positions, selection.Object.Transform, worldDelta);

    private static NumericsVector3[] OffsetPositions(
        NumericsVector3[] start,
        Transform3D transform,
        Vector3 worldDelta
    )
    {
        Vector3 localDelta = transform.Basis.Inverse() * worldDelta;
        var positions = new NumericsVector3[start.Length];
        var numericDelta = new NumericsVector3(localDelta.X, localDelta.Y, localDelta.Z);
        for (int index = 0; index < positions.Length; index++)
            positions[index] = start[index] + numericDelta;
        return positions;
    }

    private sealed record ComponentDrag(ComponentVertexSelection Selection, GDMesh Preview);

    private sealed record ExtrusionDrag(
        FaceExtrusionPlan Plan,
        GDMesh Preview,
        int[] CapVertices,
        NumericsVector3[] StartPositions
    );
}
