using System;
using System.Collections.Generic;
using Godot;
using TRMesh.Mesh;
using NumericsVector3 = System.Numerics.Vector3;

public sealed partial class PickingService
{
    public const float DefaultComponentPaddingPixels = 8.0f;

    // Component picking is deliberately CPU-side. It uses compact cached topology arrays and never
    // creates a Godot object per vertex, edge, or face.
    public ComponentPickingHit? PickComponent(
        Camera3D camera,
        Vector2 screenPosition,
        ComponentMask components,
        float paddingPixels = DefaultComponentPaddingPixels,
        bool xRay = false
    ) =>
        TryPickComponent(camera, screenPosition, components, out ComponentPickingHit hit, paddingPixels, xRay)
            ? hit
            : null;

    public bool TryPickComponent(
        Camera3D camera,
        Vector2 screenPosition,
        ComponentMask components,
        out ComponentPickingHit hit,
        float paddingPixels = DefaultComponentPaddingPixels,
        bool xRay = false
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(camera);
        if ((components & ~ComponentMask.All) != 0 || components == ComponentMask.None)
            throw new ArgumentOutOfRangeException(nameof(components));
        if (!float.IsFinite(paddingPixels) || paddingPixels < 0.0f)
            throw new ArgumentOutOfRangeException(nameof(paddingPixels));

        Vector3 rayOrigin = camera.ProjectRayOrigin(screenPosition);
        Vector3 rayDirection = camera.ProjectRayNormal(screenPosition).Normalized();
        Vector2 paddedScreenPosition = screenPosition + new Vector2(paddingPixels, 0.0f);
        Vector3 paddingRayOrigin = camera.ProjectRayOrigin(paddedScreenPosition);
        Vector3 paddingRayDirection = camera.ProjectRayNormal(paddedScreenPosition).Normalized();

        // Outside X-Ray mode, the closest surface limits vertex and edge candidates to visible
        // geometry. A small depth tolerance still admits components lying on that surface.
        bool hasSurface = TryPickNearestFace(
            rayOrigin,
            rayDirection,
            out ComponentPickingHit surfaceHit
        );
        float visibleDistance = !xRay && hasSurface
            ? surfaceHit.Distance
            : float.PositiveInfinity;

        // Priority is categorical, not merely a distance tie-break: in Free/All mode any visible
        // padded vertex wins over an edge, and any edge wins over a face.
        if (
            components.HasFlag(ComponentMask.Vertex)
            && TryPickVertex(
                rayOrigin,
                rayDirection,
                paddingRayOrigin,
                paddingRayDirection,
                visibleDistance,
                out hit
            )
        )
        {
            return true;
        }

        if (
            components.HasFlag(ComponentMask.Edge)
            && TryPickEdge(
                rayOrigin,
                rayDirection,
                paddingRayOrigin,
                paddingRayDirection,
                visibleDistance,
                out hit
            )
        )
        {
            return true;
        }

        if (components.HasFlag(ComponentMask.Face) && hasSurface)
        {
            hit = surfaceHit;
            return true;
        }

        hit = default;
        return false;
    }

    private bool TryPickVertex(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        Vector3 paddingRayOrigin,
        Vector3 paddingRayDirection,
        float visibleDistance,
        out ComponentPickingHit hit
    )
    {
        bool found = false;
        hit = default;

        foreach ((MeshObjectId objectId, PickingEntry entry) in _entries)
        {
            Transform3D transform = entry.Body.GlobalTransform;
            foreach (VertexComponent vertex in entry.ComponentGeometry.Vertices)
            {
                Vector3 position = transform * vertex.Position;
                float distance = rayDirection.Dot(position - rayOrigin);
                if (
                    distance < 0.0f
                    || distance > RayLength
                    || IsOccluded(distance, visibleDistance)
                )
                {
                    continue;
                }

                Vector3 pointOnRay = rayOrigin + (rayDirection * distance);
                float paddingSquared = PaddingRadiusSquared(
                    pointOnRay,
                    paddingRayOrigin,
                    paddingRayDirection,
                    distance
                );
                if (position.DistanceSquaredTo(pointOnRay) > paddingSquared)
                    continue;

                var candidate = new ComponentPickingHit(
                    new MeshComponentId(objectId, MeshComponentType.Vertex, vertex.Slot),
                    position,
                    distance
                );
                if (!found || IsBetter(candidate, hit))
                {
                    found = true;
                    hit = candidate;
                }
            }
        }

        return found;
    }

    private bool TryPickEdge(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        Vector3 paddingRayOrigin,
        Vector3 paddingRayDirection,
        float visibleDistance,
        out ComponentPickingHit hit
    )
    {
        bool found = false;
        hit = default;

        foreach ((MeshObjectId objectId, PickingEntry entry) in _entries)
        {
            Transform3D transform = entry.Body.GlobalTransform;
            foreach (EdgeComponent edge in entry.ComponentGeometry.Edges)
            {
                Vector3 start = transform * edge.Start;
                Vector3 end = transform * edge.End;
                if (
                    !TryClosestRaySegmentPoints(
                        rayOrigin,
                        rayDirection,
                        start,
                        end,
                        out float distance,
                        out Vector3 pointOnRay,
                        out Vector3 position
                    )
                    || distance > RayLength
                    || IsOccluded(distance, visibleDistance)
                )
                {
                    continue;
                }

                float paddingSquared = PaddingRadiusSquared(
                    pointOnRay,
                    paddingRayOrigin,
                    paddingRayDirection,
                    distance
                );
                if (position.DistanceSquaredTo(pointOnRay) > paddingSquared)
                    continue;

                var candidate = new ComponentPickingHit(
                    new MeshComponentId(objectId, MeshComponentType.Edge, edge.Slot),
                    position,
                    distance
                );
                if (!found || IsBetter(candidate, hit))
                {
                    found = true;
                    hit = candidate;
                }
            }
        }

        return found;
    }

    private bool TryPickNearestFace(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        out ComponentPickingHit hit
    )
    {
        bool found = false;
        hit = default;
        foreach ((MeshObjectId objectId, PickingEntry entry) in _entries)
        {
            Transform3D transform = entry.Body.GlobalTransform;
            foreach (FaceTriangle triangle in entry.ComponentGeometry.Triangles)
            {
                Vector3 a = transform * triangle.A;
                Vector3 b = transform * triangle.B;
                Vector3 c = transform * triangle.C;
                if (!TryIntersectTriangle(rayOrigin, rayDirection, a, b, c, out float distance))
                    continue;

                var candidate = new ComponentPickingHit(
                    new MeshComponentId(objectId, MeshComponentType.Face, triangle.FaceSlot),
                    rayOrigin + (rayDirection * distance),
                    distance
                );
                if (!found || IsBetter(candidate, hit))
                {
                    found = true;
                    hit = candidate;
                }
            }
        }

        return found;
    }

    private static bool IsOccluded(float distance, float surfaceDistance)
    {
        float tolerance = 0.0001f * MathF.Max(1.0f, surfaceDistance);
        return distance > surfaceDistance + tolerance;
    }

    private static bool IsBetter(ComponentPickingHit candidate, ComponentPickingHit current)
    {
        const float tieTolerance = 0.00001f;
        if (candidate.Distance < current.Distance - tieTolerance)
            return true;
        if (MathF.Abs(candidate.Distance - current.Distance) > tieTolerance)
            return false;
        if (candidate.Component.ObjectId.Value != current.Component.ObjectId.Value)
            return candidate.Component.ObjectId.Value < current.Component.ObjectId.Value;
        return candidate.Component.Slot < current.Component.Slot;
    }

    private static float PaddingRadiusSquared(
        Vector3 pointOnRay,
        Vector3 paddingRayOrigin,
        Vector3 paddingRayDirection,
        float distance
    )
    {
        Vector3 pointOnPaddingRay = paddingRayOrigin + (paddingRayDirection * distance);
        return pointOnRay.DistanceSquaredTo(pointOnPaddingRay);
    }

    private static bool TryClosestRaySegmentPoints(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        Vector3 segmentStart,
        Vector3 segmentEnd,
        out float rayDistance,
        out Vector3 pointOnRay,
        out Vector3 pointOnSegment
    )
    {
        const float epsilon = 0.000001f;
        Vector3 segment = segmentEnd - segmentStart;
        float segmentLengthSquared = segment.LengthSquared();
        Vector3 startFromRay = segmentStart - rayOrigin;
        if (segmentLengthSquared <= epsilon)
        {
            rayDistance = MathF.Max(0.0f, rayDirection.Dot(startFromRay));
            pointOnRay = rayOrigin + (rayDirection * rayDistance);
            pointOnSegment = segmentStart;
            return true;
        }

        float directionDotSegment = rayDirection.Dot(segment);
        float directionDotStart = rayDirection.Dot(startFromRay);
        float segmentDotStart = segment.Dot(startFromRay);
        float denominator = segmentLengthSquared
            - (directionDotSegment * directionDotSegment);
        float segmentFraction = denominator > epsilon
            ? ((directionDotSegment * directionDotStart) - segmentDotStart) / denominator
            : 0.0f;
        segmentFraction = Math.Clamp(segmentFraction, 0.0f, 1.0f);
        rayDistance = directionDotStart + (directionDotSegment * segmentFraction);
        if (rayDistance < 0.0f)
        {
            rayDistance = 0.0f;
            segmentFraction = Math.Clamp(
                -segmentDotStart / segmentLengthSquared,
                0.0f,
                1.0f
            );
        }

        pointOnRay = rayOrigin + (rayDirection * rayDistance);
        pointOnSegment = segmentStart + (segment * segmentFraction);
        return true;
    }

    private static bool TryIntersectTriangle(
        Vector3 origin,
        Vector3 direction,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        out float distance
    )
    {
        const float epsilon = 0.000001f;
        Vector3 edge1 = b - a;
        Vector3 edge2 = c - a;
        Vector3 p = direction.Cross(edge2);
        float determinant = edge1.Dot(p);
        if (MathF.Abs(determinant) < epsilon)
        {
            distance = 0.0f;
            return false;
        }

        float inverse = 1.0f / determinant;
        Vector3 t = origin - a;
        float u = t.Dot(p) * inverse;
        if (u < 0.0f || u > 1.0f)
        {
            distance = 0.0f;
            return false;
        }

        Vector3 q = t.Cross(edge1);
        float v = direction.Dot(q) * inverse;
        if (v < 0.0f || u + v > 1.0f)
        {
            distance = 0.0f;
            return false;
        }

        distance = edge2.Dot(q) * inverse;
        return distance >= 0.0f && distance <= RayLength;
    }

    private sealed partial class PickingEntry
    {
        public ComponentGeometry ComponentGeometry { get; private set; } = ComponentGeometry.Empty;

        public void UpdateComponentGeometry(GDMesh mesh)
        {
            SpatialMesh spatialMesh = mesh.SpatialMesh;
            var vertices = new List<VertexComponent>(spatialMesh.VertexCount);
            spatialMesh.WithVertices((slots, _, positions) =>
            {
                for (int i = 0; i < slots.Length; i++)
                    vertices.Add(new VertexComponent(slots[i], ToGodot(positions[i])));
            });

            var edges = new List<EdgeComponent>(spatialMesh.EdgeCount);
            spatialMesh.WithHalfEdges((slots, topology, _) =>
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    HalfEdge halfEdge = topology[i];
                    if (slots[i] > halfEdge.TwinHalfEdge)
                        continue;

                    NumericsVector3 start = spatialMesh.GetVertexData(halfEdge.SourceVertex).position;
                    int endVertex = spatialMesh
                        .GetHalfEdgeData(halfEdge.TwinHalfEdge)
                        .topology.SourceVertex;
                    NumericsVector3 end = spatialMesh.GetVertexData(endVertex).position;
                    edges.Add(new EdgeComponent(slots[i], ToGodot(start), ToGodot(end)));
                }
            });

            MeshSurface surface = mesh.Surface;
            var triangles = new FaceTriangle[surface.FaceSlots.Count];
            for (int triangle = 0; triangle < triangles.Length; triangle++)
            {
                int element = triangle * 3;
                triangles[triangle] = new FaceTriangle(
                    surface.FaceSlots[triangle],
                    ToGodot(surface.Vertices[surface.ElementIndices[element]]),
                    ToGodot(surface.Vertices[surface.ElementIndices[element + 1]]),
                    ToGodot(surface.Vertices[surface.ElementIndices[element + 2]])
                );
            }

            ComponentGeometry = new ComponentGeometry(vertices.ToArray(), edges.ToArray(), triangles);
        }

        private static Vector3 ToGodot(NumericsVector3 value) => new(value.X, value.Y, value.Z);
    }

    private sealed record ComponentGeometry(
        VertexComponent[] Vertices,
        EdgeComponent[] Edges,
        FaceTriangle[] Triangles
    )
    {
        public static readonly ComponentGeometry Empty = new([], [], []);

        public Vector3[] ToCollisionFaces()
        {
            var faces = new Vector3[Triangles.Length * 3];
            for (int i = 0; i < Triangles.Length; i++)
            {
                FaceTriangle triangle = Triangles[i];
                int offset = i * 3;
                faces[offset] = triangle.A;
                faces[offset + 1] = triangle.B;
                faces[offset + 2] = triangle.C;
            }

            return faces;
        }
    }

    private readonly record struct VertexComponent(int Slot, Vector3 Position);

    private readonly record struct EdgeComponent(int Slot, Vector3 Start, Vector3 End);

    private readonly record struct FaceTriangle(int FaceSlot, Vector3 A, Vector3 B, Vector3 C);
}
