using System;
using Godot;
using TREditorSharp;
using NumericsVector3 = System.Numerics.Vector3;

public sealed class EdgeCutToolInput
{
    private readonly EditorToolContext _context;
    private PendingCut? _pending;

    public EdgeCutToolInput(EditorToolContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public void Reset()
    {
        _pending = null;
    }

    public EditorToolResult HandleMouseButton(ViewportMouseButtonEvent input)
    {
        if (input.Button != MouseButton.Left || !input.Pressed)
            return EditorToolResult.Continue;

        if (!TryGetSelectedFace(out SelectionTarget face))
        {
            Reset();
            _context.ReportStatus("Select one face to cut.");
            return SelectionToolInput.HandleMouseButton(
                _context,
                input,
                ScenePickElementFilter.Face
            );
        }

        if (_pending is PendingCut pending && pending.Face != face)
            Reset();

        if (!TryPickCutPoint(face, input.RayOrigin, input.RayDirection, out CutPoint cutPoint))
        {
            return EditorToolResult.Continue;
        }

        if (_pending == null)
        {
            _pending = new PendingCut(face, cutPoint);
            string cancelBinding =
                KeybindingService.Instance?.GetBindingDisplayText(KeybindingActions.Cancel)
                ?? "The cancel binding";
            _context.ReportStatus(
                $"Choose a second edge or vertex on the selected face. {cancelBinding} cancels."
            );
            return EditorToolResult.ContinueWithPreview(CreatePreview(cutPoint, cutPoint, false));
        }

        PendingCut first = _pending.Value;
        if (!CanConnect(face, first.Point, cutPoint))
            return EditorToolResult.Continue;

        EdgeCutCommand command = EdgeCutCommand.Create(
            _context.Selection.Current,
            first.Point.Edge,
            first.Point.Parameter,
            cutPoint.Edge,
            cutPoint.Parameter
        );
        Reset();
        _context.ReportStatus("Edge cut complete.");
        return command == null
            ? EditorToolResult.ContinueWithPreview(new EditorPreviewRequest.Clear())
            : new EditorToolResult(
                EditorToolStatus.Continue,
                command,
                new EditorPreviewRequest.Clear()
            );
    }

    public EditorToolResult HandleMouseMotion(ViewportMouseMotionEvent input)
    {
        if (!TryGetSelectedFace(out SelectionTarget face))
        {
            Reset();
            UpdateFaceHover(input);
            _context.ReportStatus("Select one face to cut.");
            return EditorToolResult.ContinueWithPreview(new EditorPreviewRequest.Clear());
        }

        if (_pending is PendingCut pending && pending.Face != face)
            Reset();

        if (_pending == null)
        {
            UpdateCutPointHover(face, input, excludedPoint: null);
            _context.ReportStatus("Click the first edge or vertex of the cut.");
            return EditorToolResult.Continue;
        }

        PendingCut first = _pending.Value;
        if (
            !_context.TryGetObject(first.Point.ObjectId, out EditorObjectModel firstObject)
            || !firstObject.Mesh.IsFaceAlive(face.Face)
            || !firstObject.Mesh.IsHalfEdgeAlive(first.Point.Edge)
            || first.Point.ObjectId != face.ObjectId
        )
        {
            Reset();
            return EditorToolResult.ContinueWithPreview(new EditorPreviewRequest.Clear());
        }

        bool hasValidTarget =
            TryPickCutPoint(face, input.RayOrigin, input.RayDirection, out CutPoint target)
            && CanConnect(face, first.Point, target);
        Vector3 end = hasValidTarget
            ? target.Position
            : ProjectRayOntoFace(
                firstObject.Mesh,
                _context.GetObjectGlobalTransform(firstObject),
                face.Face,
                input.RayOrigin,
                input.RayDirection,
                first.Point.Position
            );

        SelectionTarget? hover = hasValidTarget
            ? CreateSelectionTarget(face.ObjectId, target)
            : null;
        _context.ComponentSelectionHighlight.SetPointerState(input.RayOrigin, hover);
        return EditorToolResult.ContinueWithPreview(
            CreatePreview(first.Point, end, hasValidTarget)
        );
    }

    public EditorToolResult HandleAction(EditorInputAction action)
    {
        if (action != EditorInputAction.Cancel || _pending == null)
            return EditorToolResult.Continue;

        Reset();
        _context.ReportStatus("Click the first edge or vertex of the cut.");
        return EditorToolResult.ContinueWithPreview(new EditorPreviewRequest.Clear());
    }

    private void UpdateFaceHover(ViewportMouseMotionEvent input)
    {
        SelectionTarget? hover = null;
        if (
            _context.ScenePicking.TryPickScene(
                input.RayOrigin,
                input.RayDirection,
                out ScenePickHit hit,
                ScenePickElementFilter.Face
            ) && SelectionTarget.TryFromHit(hit, out SelectionTarget target)
        )
        {
            hover = target;
        }
        _context.ComponentSelectionHighlight.SetPointerState(input.RayOrigin, hover);
    }

    private void UpdateCutPointHover(
        SelectionTarget face,
        ViewportMouseMotionEvent input,
        CutPoint? excludedPoint
    )
    {
        SelectionTarget? hover = null;
        if (
            TryPickCutPoint(face, input.RayOrigin, input.RayDirection, out CutPoint point)
            && (excludedPoint == null || CanConnect(face, excludedPoint.Value, point))
        )
        {
            hover = CreateSelectionTarget(face.ObjectId, point);
        }
        _context.ComponentSelectionHighlight.SetPointerState(input.RayOrigin, hover);
    }

    private bool TryPickCutPoint(
        SelectionTarget face,
        Vector3 rayOrigin,
        Vector3 rayDirection,
        out CutPoint point
    )
    {
        point = default;
        if (
            !_context.TryGetObject(face.ObjectId, out EditorObjectModel obj)
            || !obj.Mesh.IsFaceAlive(face.Face)
        )
        {
            return false;
        }

        Transform3D inverse = _context.GetObjectGlobalTransform(obj).AffineInverse();
        Vector3 localOrigin = inverse * rayOrigin;
        Vector3 localDirection = inverse.Basis * rayDirection;
        if (localDirection.IsZeroApprox())
            return false;
        localDirection = localDirection.Normalized();

        SpatialMesh mesh = obj.Mesh;
        float vertexRadiusSquared =
            _context.ScenePicking.VertexPickRadius * _context.ScenePicking.VertexPickRadius;
        float bestVertexRayDistance = float.MaxValue;
        HalfEdgeHandle bestVertexEdge = HalfEdgeHandle.Null;
        VertexHandle bestVertex = VertexHandle.Null;
        Vector3 bestVertexPosition = default;
        foreach (FaceCornerHandle edge in mesh.HalfEdgesAroundFace(face.Face))
        {
            VertexHandle vertex = mesh.GetHalfEdge(edge).Origin;
            Vector3 vertexPosition = ToGodot(mesh.GetVertexPosition(vertex));
            float rayDistance = (vertexPosition - localOrigin).Dot(localDirection);
            Vector3 rayPoint = localOrigin + localDirection * rayDistance;
            if (
                rayDistance < 0f
                || (vertexPosition - rayPoint).LengthSquared() > vertexRadiusSquared
                || rayDistance >= bestVertexRayDistance
            )
            {
                continue;
            }

            bestVertexRayDistance = rayDistance;
            bestVertexEdge = edge;
            bestVertex = vertex;
            bestVertexPosition = vertexPosition;
        }

        if (!bestVertex.IsNull)
        {
            point = new CutPoint(obj.Id, bestVertexEdge, 0f, bestVertex, bestVertexPosition);
            return true;
        }

        float radiusSquared =
            _context.ScenePicking.EdgePickRadius * _context.ScenePicking.EdgePickRadius;
        float bestRayDistance = float.MaxValue;
        HalfEdgeHandle bestEdge = HalfEdgeHandle.Null;
        float bestParameter = 0f;
        Vector3 bestPosition = default;
        foreach (FaceCornerHandle edge in mesh.HalfEdgesAroundFace(face.Face))
        {
            HalfEdge data = mesh.GetHalfEdge(edge);
            Vector3 a = ToGodot(mesh.GetVertexPosition(data.Origin));
            Vector3 b = ToGodot(mesh.GetVertexPosition(mesh.GetHalfEdge(data.Twin).Origin));
            RaySegmentClosestPoints closest = RaySegmentGeometry.FindClosestPoints(
                localOrigin,
                localDirection,
                a,
                b
            );
            if (
                closest.RayDistance < 0f
                || (closest.RayPoint - closest.SegmentPoint).LengthSquared() > radiusSquared
                || closest.RayDistance >= bestRayDistance
            )
            {
                continue;
            }

            bestRayDistance = closest.RayDistance;
            bestEdge = edge;
            bestParameter = closest.SegmentParameter;
            bestPosition = closest.SegmentPoint;
        }

        if (bestEdge.IsNull)
            return false;

        bestParameter = Mathf.Clamp(
            bestParameter,
            EdgeCutChange.MinimumEdgeParameter,
            1f - EdgeCutChange.MinimumEdgeParameter
        );
        HalfEdge bestData = mesh.GetHalfEdge(bestEdge);
        Vector3 bestStart = ToGodot(mesh.GetVertexPosition(bestData.Origin));
        Vector3 bestEnd = ToGodot(mesh.GetVertexPosition(mesh.GetHalfEdge(bestData.Twin).Origin));
        bestPosition = bestStart.Lerp(bestEnd, bestParameter);
        point = new CutPoint(obj.Id, bestEdge, bestParameter, VertexHandle.Null, bestPosition);
        return true;
    }

    private bool TryGetSelectedFace(out SelectionTarget face)
    {
        SelectionSnapshot selection = _context.Selection.Current;
        if (selection.Count == 1 && selection.Targets[0].Kind == ScenePickElementKind.Face)
        {
            face = selection.Targets[0];
            return true;
        }

        face = default;
        return false;
    }

    private static Vector3 ProjectRayOntoFace(
        SpatialMesh mesh,
        Transform3D globalTransform,
        FaceHandle face,
        Vector3 rayOrigin,
        Vector3 rayDirection,
        Vector3 fallback
    )
    {
        Transform3D inverse = globalTransform.AffineInverse();
        Vector3 localOrigin = inverse * rayOrigin;
        Vector3 localDirection = inverse.Basis * rayDirection;
        Vector3 normal = ToGodot(mesh.ComputeFaceNormal(face));
        Vector3 pointOnPlane = ToGodot(mesh.ComputeFaceCentroid(face));
        float denominator = normal.Dot(localDirection);
        if (Mathf.Abs(denominator) <= Mathf.Epsilon)
            return fallback;

        float distance = normal.Dot(pointOnPlane - localOrigin) / denominator;
        return distance >= 0f ? localOrigin + localDirection * distance : fallback;
    }

    private EditorPreviewRequest.EdgeCut CreatePreview(
        CutPoint first,
        CutPoint end,
        bool hasValidTarget
    ) => CreatePreview(first, end.Position, hasValidTarget);

    private EditorPreviewRequest.EdgeCut CreatePreview(
        CutPoint first,
        Vector3 end,
        bool hasValidTarget
    ) => new(GetMeshTransform(first), first.Position, end, hasValidTarget);

    private Transform3D GetMeshTransform(CutPoint point) =>
        _context.TryGetObject(point.ObjectId, out EditorObjectModel obj)
            ? _context.GetObjectGlobalTransform(obj)
            : Transform3D.Identity;

    private bool CanConnect(SelectionTarget face, CutPoint first, CutPoint second) =>
        _context.TryGetObject(first.ObjectId, out EditorObjectModel obj)
        && EdgeCutChange.CanCut(
            obj.Mesh,
            face.Face,
            first.Edge,
            first.Parameter,
            second.Edge,
            second.Parameter
        );

    private static SelectionTarget CreateSelectionTarget(EditorObjectId objectId, CutPoint point) =>
        point.Vertex.IsNull
            ? SelectionTarget.ForEdge(objectId, point.Edge)
            : SelectionTarget.ForVertex(objectId, point.Vertex);

    private static Vector3 ToGodot(NumericsVector3 value) => new(value.X, value.Y, value.Z);

    private readonly record struct PendingCut(SelectionTarget Face, CutPoint Point);

    private readonly record struct CutPoint(
        EditorObjectId ObjectId,
        HalfEdgeHandle Edge,
        float Parameter,
        VertexHandle Vertex,
        Vector3 Position
    );
}
