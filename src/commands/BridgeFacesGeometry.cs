using System;
using System.Collections.Generic;
using Godot;
using TRMesh.Mesh;
using NumericsVector2 = System.Numerics.Vector2;
using NumericsVector3 = System.Numerics.Vector3;

internal sealed record BridgeFaceLoop(
    MeshObject Object,
    int Face,
    int[] Vertices,
    Vector3[] WorldPositions,
    Vector3 Centroid,
    Vector3 Normal
);

internal sealed record BridgeFacesPlan(
    BridgeFaceLoop First,
    BridgeFaceLoop Second,
    Vector3[] MatchedSecondPositions,
    Transform3D BridgeTransform
);

internal static class BridgeFacesGeometry
{
    private const float DegenerateEpsilonSquared = 1e-10f;

    public static BridgeFacesPlan Resolve(
        IEditorDocumentReader document,
        SelectionValue selection
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        if (selection.ObjectIds.Count != 0)
            throw new ArgumentException("Deselect all objects; Bridge Faces only accepts face selections.");
        if (selection.OrderedComponents.Count != 2)
            throw new ArgumentException("Select exactly two faces with the same number of edges.");

        MeshComponentId firstId = selection.OrderedComponents[0];
        MeshComponentId secondId = selection.OrderedComponents[1];
        if (firstId.Type != MeshComponentType.Face || secondId.Type != MeshComponentType.Face)
            throw new ArgumentException("Only faces can be bridged; deselect all selected vertices and edges.");
        if (firstId == secondId)
            throw new ArgumentException("Select two distinct faces to bridge.");
        if (
            !document.TryGetObject(firstId.ObjectId, out MeshObject firstObject)
            || !document.TryGetObject(secondId.ObjectId, out MeshObject secondObject)
        )
        {
            throw new ArgumentException("The selected faces are missing or stale and cannot be bridged.");
        }

        BridgeFaceLoop first = ReadLoop(firstObject, firstId.Slot);
        BridgeFaceLoop second = ReadLoop(secondObject, secondId.Slot);
        if (first.WorldPositions.Length != second.WorldPositions.Length)
            throw new ArgumentException("Bridge Faces requires faces with the same number of edges.");
        if (
            ReferenceEquals(firstObject, secondObject)
            && new HashSet<int>(first.Vertices).Overlaps(second.Vertices)
        )
            throw new ArgumentException("Faces that share a vertex or edge cannot be bridged.");

        Vector3[] matched = MatchLoop(first, second);
        return new BridgeFacesPlan(
            first,
            second,
            matched,
            new Transform3D(Basis.Identity, first.Centroid)
        );
    }

    public static Curve3D CreateQuadraticCurve(BridgeFacesPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        Vector3 start = Vector3.Zero;
        Vector3 end = plan.Second.Centroid - plan.First.Centroid;
        Vector3 midpoint = (start + end) * 0.5f;
        var curve = new Curve3D { UpVectorEnabled = false };
        curve.AddPoint(start);
        curve.AddPoint(midpoint);
        curve.AddPoint(end);
        SynchronizeQuadraticTangents(curve);
        return curve;
    }

    // Curve3D is cubic, so split one quadratic at t=.5 into two exactly equivalent cubics. The
    // middle anchor is the single shape handle; all tangent handles stay derived and locked.
    public static void SynchronizeQuadraticTangents(Curve3D curve)
    {
        ArgumentNullException.ThrowIfNull(curve);
        if (curve.PointCount != 3)
            throw new ArgumentException("A bridge curve must contain three anchors.", nameof(curve));

        Vector3 start = curve.GetPointPosition(0);
        Vector3 midpoint = curve.GetPointPosition(1);
        Vector3 end = curve.GetPointPosition(2);
        Vector3 control = 2.0f * midpoint - 0.5f * (start + end);

        curve.SetPointIn(0, Vector3.Zero);
        curve.SetPointOut(0, (control - start) / 3.0f);
        curve.SetPointIn(1, (start - end) / 6.0f);
        curve.SetPointOut(1, (end - start) / 6.0f);
        curve.SetPointIn(2, (control - end) / 3.0f);
        curve.SetPointOut(2, Vector3.Zero);
    }

    public static Vector3[][] CreateRings(
        BridgeFacesPlan plan,
        Curve3D curve,
        int intermediateSteps
    )
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(curve);
        ArgumentOutOfRangeException.ThrowIfNegative(intermediateSteps);

        int edgeCount = plan.First.WorldPositions.Length;
        int ringCount = checked(intermediateSteps + 2);
        var first = ToBridgeLocal(plan.First.WorldPositions, plan.BridgeTransform.Origin);
        var second = ToBridgeLocal(plan.MatchedSecondPositions, plan.BridgeTransform.Origin);
        Vector3 secondCenter = plan.Second.Centroid - plan.First.Centroid;
        (Basis firstFrame, Basis secondFrame) = CreateEndpointFrames(
            plan,
            first,
            second,
            secondCenter
        );
        Vector3[] firstShape = ToFrameCoordinates(first, Vector3.Zero, firstFrame);
        Vector3[] secondShape = ToFrameCoordinates(second, secondCenter, secondFrame);
        var firstRotation = firstFrame.GetRotationQuaternion();
        var secondRotation = secondFrame.GetRotationQuaternion();
        float curveLength = curve.GetBakedLength();
        var rings = new Vector3[ringCount][];

        for (int ring = 0; ring < ringCount; ring++)
        {
            float fraction = (float)ring / (ringCount - 1);
            Vector3 center = curve.SampleBaked(curveLength * fraction, cubic: true);
            Basis frame = new(firstRotation.Slerp(secondRotation, fraction));
            var positions = new Vector3[edgeCount];
            for (int corner = 0; corner < edgeCount; corner++)
            {
                Vector3 shape = firstShape[corner].Lerp(secondShape[corner], fraction);
                positions[corner] = center + frame * shape;
                if (!positions[corner].IsFinite())
                    throw new ArgumentException("The bridge produced a non-finite vertex position.");
            }
            if (ring == 0)
                first.CopyTo(positions, 0);
            else if (ring == ringCount - 1)
                second.CopyTo(positions, 0);
            rings[ring] = positions;
        }

        ValidateQuads(rings);
        return rings;
    }

    public static SpatialMesh BuildMesh(Vector3[][] rings, out int[] faces)
    {
        ArgumentNullException.ThrowIfNull(rings);
        if (rings.Length < 2 || rings[0].Length < 3)
            throw new ArgumentException("A bridge needs at least two rings with three corners.", nameof(rings));
        ValidateQuads(rings);

        int edgeCount = rings[0].Length;
        var mesh = new SpatialMesh();
        try
        {
            var vertices = new int[rings.Length][];
            for (int ring = 0; ring < rings.Length; ring++)
            {
                vertices[ring] = new int[edgeCount];
                for (int corner = 0; corner < edgeCount; corner++)
                    vertices[ring][corner] = mesh.AddVertex(ToNumerics(rings[ring][corner]));
            }

            faces = new int[(rings.Length - 1) * edgeCount];
            int faceIndex = 0;
            for (int ring = 0; ring < rings.Length - 1; ring++)
            {
                float v0 = (float)ring / (rings.Length - 1);
                float v1 = (float)(ring + 1) / (rings.Length - 1);
                for (int corner = 0; corner < edgeCount; corner++)
                {
                    int next = (corner + 1) % edgeCount;
                    float u0 = (float)corner / edgeCount;
                    float u1 = (float)(corner + 1) / edgeCount;
                    faces[faceIndex++] = mesh.AddFace(
                        [
                            vertices[ring][corner],
                            vertices[ring][next],
                            vertices[ring + 1][next],
                            vertices[ring + 1][corner],
                        ],
                        [
                            new NumericsVector2(u0, v0),
                            new NumericsVector2(u1, v0),
                            new NumericsVector2(u1, v1),
                            new NumericsVector2(u0, v1),
                        ]
                    );
                }
            }
            return mesh;
        }
        catch
        {
            mesh.Dispose();
            throw;
        }
    }

    private static BridgeFaceLoop ReadLoop(MeshObject meshObject, int face)
    {
        SpatialMesh mesh = meshObject.Mesh.SpatialMesh;
        if (!mesh.IsFaceAlive(face))
            throw new ArgumentException("The selected faces are missing or stale and cannot be bridged.");
        float determinant = meshObject.Transform.Basis.Determinant();
        if (!float.IsFinite(determinant) || Mathf.IsZeroApprox(determinant))
            throw new ArgumentException("A source mesh transform must be finite and invertible.");

        var vertices = new List<int>();
        var positions = new List<Vector3>();
        int start = mesh.GetFaceData(face).topology.AdjacentHalfEdge;
        int current = start;
        do
        {
            if (!mesh.IsHalfEdgeAlive(current) || vertices.Count > mesh.HalfEdgeCount)
                throw new ArgumentException("The selected face boundary is invalid.");
            HalfEdge edge = mesh.GetHalfEdgeData(current).topology;
            NumericsVector3 local = mesh.GetVertexData(edge.SourceVertex).position;
            Vector3 world = meshObject.Transform * new Vector3(local.X, local.Y, local.Z);
            if (!world.IsFinite())
                throw new ArgumentException("A selected face contains a non-finite position.");
            vertices.Add(edge.SourceVertex);
            positions.Add(world);
            current = edge.NextHalfEdge;
        } while (current != start);

        if (positions.Count < 3)
            throw new ArgumentException("Each bridged face must have at least three edges.");
        Vector3 centroid = Centroid(positions);
        Vector3 area = Vector3.Zero;
        for (int index = 0; index < positions.Count; index++)
        {
            Vector3 a = positions[index] - centroid;
            Vector3 b = positions[(index + 1) % positions.Count] - centroid;
            if ((b - a).LengthSquared() <= DegenerateEpsilonSquared)
                throw new ArgumentException("A selected face has a degenerate boundary edge.");
            area += a.Cross(b);
        }
        for (int first = 0; first < positions.Count; first++)
        {
            for (int second = first + 1; second < positions.Count; second++)
            {
                if (positions[first].DistanceSquaredTo(positions[second]) <= DegenerateEpsilonSquared)
                    throw new ArgumentException("A selected face contains coincident corners.");
            }
        }
        if (area.LengthSquared() <= DegenerateEpsilonSquared)
            throw new ArgumentException("A selected face is degenerate.");

        return new BridgeFaceLoop(
            meshObject,
            face,
            [.. vertices],
            [.. positions],
            centroid,
            area.Normalized()
        );
    }

    private static Vector3[] MatchLoop(BridgeFaceLoop first, BridgeFaceLoop second)
    {
        int count = first.WorldPositions.Length;
        (_, _, bool reverse) = CanonicalNormals(first, second);
        float bestCost = float.PositiveInfinity;
        int bestOffset = 0;
        for (int offset = 0; offset < count; offset++)
        {
            float cost = 0.0f;
            for (int index = 0; index < count; index++)
            {
                int candidate = reverse
                    ? (offset - index + count) % count
                    : (index + offset) % count;
                Vector3 firstOffset = first.WorldPositions[index] - first.Centroid;
                Vector3 secondOffset = second.WorldPositions[candidate] - second.Centroid;
                cost += firstOffset.DistanceSquaredTo(secondOffset);
            }
            if (cost < bestCost)
            {
                bestCost = cost;
                bestOffset = offset;
            }
        }

        var matched = new Vector3[count];
        for (int index = 0; index < count; index++)
        {
            int candidate = reverse
                ? (bestOffset - index + count) % count
                : (index + bestOffset) % count;
            matched[index] = second.WorldPositions[candidate];
        }
        return matched;
    }

    private static (Basis First, Basis Second) CreateEndpointFrames(
        BridgeFacesPlan plan,
        Vector3[] first,
        Vector3[] second,
        Vector3 secondCenter
    )
    {
        (Vector3 firstNormal, Vector3 secondNormal, _) = CanonicalNormals(
            plan.First,
            plan.Second
        );
        int referenceCorner = 0;
        float bestScore = -1.0f;
        for (int corner = 0; corner < first.Length; corner++)
        {
            Vector3 firstRadial = first[corner].Slide(firstNormal);
            Vector3 secondRadial = (second[corner] - secondCenter).Slide(secondNormal);
            float score = Mathf.Min(firstRadial.LengthSquared(), secondRadial.LengthSquared());
            if (score > bestScore)
            {
                bestScore = score;
                referenceCorner = corner;
            }
        }
        if (bestScore <= DegenerateEpsilonSquared)
            throw new ArgumentException("A selected face cannot define a stable bridge frame.");

        return (
            CreateFrame(firstNormal, first[referenceCorner]),
            CreateFrame(secondNormal, second[referenceCorner] - secondCenter)
        );
    }

    // Face normals are oriented, but a cross-section plane is not. Pick the normal signs that face
    // generally along the bridge and are closest to each other. A sign change reverses that face's
    // perimeter winding, which is the only admissible loop orientation for correspondence.
    private static (Vector3 First, Vector3 Second, bool Reverse) CanonicalNormals(
        BridgeFaceLoop first,
        BridgeFaceLoop second
    )
    {
        Vector3 chord = second.Centroid - first.Centroid;
        bool flipFirst = first.Normal.Dot(chord) < 0.0f;
        bool flipSecond = second.Normal.Dot(chord) < 0.0f;
        Vector3 firstNormal = flipFirst ? -first.Normal : first.Normal;
        Vector3 secondNormal = flipSecond ? -second.Normal : second.Normal;
        if (firstNormal.Dot(secondNormal) < 0.0f)
        {
            secondNormal = -secondNormal;
            flipSecond = !flipSecond;
        }
        return (firstNormal, secondNormal, flipFirst != flipSecond);
    }

    private static Basis CreateFrame(Vector3 normal, Vector3 radial)
    {
        Vector3 x = radial.Slide(normal).Normalized();
        Vector3 y = normal.Cross(x).Normalized();
        return new Basis(x, y, normal).Orthonormalized();
    }

    private static Vector3[] ToFrameCoordinates(
        Vector3[] positions,
        Vector3 center,
        Basis frame
    )
    {
        Basis inverse = frame.Transposed();
        var coordinates = new Vector3[positions.Length];
        for (int index = 0; index < positions.Length; index++)
            coordinates[index] = inverse * (positions[index] - center);
        return coordinates;
    }

    private static void ValidateQuads(Vector3[][] rings)
    {
        int edgeCount = rings[0].Length;
        for (int ring = 0; ring < rings.Length - 1; ring++)
        {
            if (rings[ring].Length != edgeCount || rings[ring + 1].Length != edgeCount)
                throw new ArgumentException("Every bridge ring must have the same corner count.");
            for (int corner = 0; corner < edgeCount; corner++)
            {
                int next = (corner + 1) % edgeCount;
                Vector3 a = rings[ring][corner];
                Vector3 b = rings[ring][next];
                Vector3 c = rings[ring + 1][next];
                Vector3 d = rings[ring + 1][corner];
                if (
                    Coincide(a, b)
                    || Coincide(a, c)
                    || Coincide(a, d)
                    || Coincide(b, c)
                    || Coincide(b, d)
                    || Coincide(c, d)
                )
                {
                    throw new ArgumentException(
                        "The requested bridge contains a degenerate quad."
                    );
                }
                float areaSquared = (b - a).Cross(c - a).LengthSquared()
                    + (c - a).Cross(d - a).LengthSquared();
                if (areaSquared <= DegenerateEpsilonSquared)
                    throw new ArgumentException("The requested bridge contains a degenerate quad.");
            }
        }
    }

    private static bool Coincide(Vector3 first, Vector3 second) =>
        first.DistanceSquaredTo(second) <= DegenerateEpsilonSquared;

    private static Vector3 Centroid(IReadOnlyList<Vector3> positions)
    {
        Vector3 centroid = Vector3.Zero;
        foreach (Vector3 position in positions)
            centroid += position;
        return centroid / positions.Count;
    }

    private static Vector3[] ToBridgeLocal(Vector3[] positions, Vector3 origin)
    {
        var local = new Vector3[positions.Length];
        for (int index = 0; index < positions.Length; index++)
            local[index] = positions[index] - origin;
        return local;
    }

    private static NumericsVector3 ToNumerics(Vector3 value) => new(value.X, value.Y, value.Z);
}
