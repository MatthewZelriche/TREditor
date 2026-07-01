using System;
using System.Collections.Generic;
using Godot;
using TREditorSharp;
using NumericsVector3 = System.Numerics.Vector3;

// Manages the simple collision test logic for picking mesh vertices, edges, and faces.
// returns TRMesh handles for the picked components
public static class MeshComponentPicker
{
    private const float Epsilon = 0.000001f;

    public static bool TryPickComponents(
        TRMeshGD meshNode,
        Vector3 rayOrigin,
        Vector3 rayDirection,
        float maxDistance,
        float vertexRadius,
        float edgeRadius,
        out ScenePickHit vertex,
        out ScenePickHit edge,
        out ScenePickHit face
    )
    {
        ArgumentNullException.ThrowIfNull(meshNode);

        vertex = PickVertex(meshNode, rayOrigin, rayDirection, maxDistance, vertexRadius);
        edge = PickEdge(meshNode, rayOrigin, rayDirection, maxDistance, edgeRadius);
        face = PickFace(meshNode, rayOrigin, rayDirection, maxDistance);
        return vertex.HasHit || edge.HasHit || face.HasHit;
    }

    private static ScenePickHit PickVertex(
        TRMeshGD meshNode,
        Vector3 rayOrigin,
        Vector3 rayDirection,
        float maxDistance,
        float radius
    )
    {
        if (radius <= 0.0f)
        {
            return ScenePickHit.None;
        }

        SpatialMesh mesh = meshNode.SourceMesh;
        float bestDistance = float.MaxValue;
        VertexHandle bestVertex = default;
        Vector3 bestPosition = default;
        float radiusSquared = radius * radius;

        foreach (VertexHandle vertex in mesh.EnumerateLiveVertices())
        {
            Vector3 vertexPosition = ToGodotVector3(mesh.GetVertexPosition(vertex));
            Vector3 toVertex = vertexPosition - rayOrigin;
            float rayDistance = toVertex.Dot(rayDirection);
            if (rayDistance < 0.0f || rayDistance > maxDistance)
            {
                continue;
            }

            Vector3 closestPoint = rayOrigin + rayDirection * rayDistance;
            if ((vertexPosition - closestPoint).LengthSquared() > radiusSquared)
            {
                continue;
            }

            if (rayDistance < bestDistance)
            {
                bestDistance = rayDistance;
                bestVertex = vertex;
                bestPosition = closestPoint;
            }
        }

        return bestDistance < float.MaxValue
            ? ScenePickHit.VertexHit(meshNode, bestVertex, bestPosition, bestDistance)
            : ScenePickHit.None;
    }

    private static ScenePickHit PickEdge(
        TRMeshGD meshNode,
        Vector3 rayOrigin,
        Vector3 rayDirection,
        float maxDistance,
        float radius
    )
    {
        if (radius <= 0.0f)
        {
            return ScenePickHit.None;
        }

        SpatialMesh mesh = meshNode.SourceMesh;
        float bestDistance = float.MaxValue;
        HalfEdgeHandle bestEdge = default;
        Vector3 bestPosition = default;
        float radiusSquared = radius * radius;

        foreach (HalfEdgeHandle edge in mesh.EnumerateLiveEdges())
        {
            HalfEdge halfEdge = mesh.GetHalfEdge(edge);
            HalfEdge twin = mesh.GetHalfEdge(halfEdge.Twin);
            Vector3 a = ToGodotVector3(mesh.GetVertexPosition(halfEdge.Origin));
            Vector3 b = ToGodotVector3(mesh.GetVertexPosition(twin.Origin));

            RaySegmentClosestPoints closest = RaySegmentGeometry.FindClosestPoints(
                rayOrigin,
                rayDirection,
                a,
                b
            );

            if (closest.RayDistance < 0.0f || closest.RayDistance > maxDistance)
            {
                continue;
            }

            if ((closest.RayPoint - closest.SegmentPoint).LengthSquared() > radiusSquared)
            {
                continue;
            }

            if (closest.RayDistance < bestDistance)
            {
                bestDistance = closest.RayDistance;
                bestEdge = edge;
                bestPosition = closest.RayPoint;
            }
        }

        return bestDistance < float.MaxValue
            ? ScenePickHit.EdgeHit(meshNode, bestEdge, bestPosition, bestDistance)
            : ScenePickHit.None;
    }

    private static ScenePickHit PickFace(
        TRMeshGD meshNode,
        Vector3 rayOrigin,
        Vector3 rayDirection,
        float maxDistance
    )
    {
        SpatialMesh mesh = meshNode.SourceMesh;
        List<FaceCornerHandle> faceTriangulation = [];
        float bestDistance = float.MaxValue;
        FaceHandle bestFace = default;

        foreach (FaceHandle face in mesh.EnumerateLiveFaces())
        {
            faceTriangulation.Clear();
            if (!mesh.TriangulateFace(face, faceTriangulation))
            {
                continue;
            }

            for (int i = 0; i < faceTriangulation.Count; i += 3)
            {
                Vector3 a = GetCornerPosition(mesh, faceTriangulation[i]);
                Vector3 b = GetCornerPosition(mesh, faceTriangulation[i + 1]);
                Vector3 c = GetCornerPosition(mesh, faceTriangulation[i + 2]);

                if (
                    TryRayTriangle(rayOrigin, rayDirection, a, b, c, out float distance)
                    && distance >= Epsilon
                    && distance <= maxDistance + Epsilon
                    && distance < bestDistance
                )
                {
                    bestDistance = distance;
                    bestFace = face;
                }
            }
        }

        return bestDistance < float.MaxValue
            ? ScenePickHit.FaceHit(
                meshNode,
                bestFace,
                rayOrigin + rayDirection * bestDistance,
                bestDistance
            )
            : ScenePickHit.None;
    }

    private static bool TryRayTriangle(
        Vector3 origin,
        Vector3 direction,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        out float distance
    )
    {
        distance = 0.0f;
        Vector3 edge1 = b - a;
        Vector3 edge2 = c - a;
        Vector3 p = direction.Cross(edge2);
        float determinant = edge1.Dot(p);
        if (Mathf.Abs(determinant) < Epsilon)
        {
            return false;
        }

        float inverseDeterminant = 1.0f / determinant;
        Vector3 t = origin - a;
        float u = t.Dot(p) * inverseDeterminant;
        if (u < 0.0f || u > 1.0f)
        {
            return false;
        }

        Vector3 q = t.Cross(edge1);
        float v = direction.Dot(q) * inverseDeterminant;
        if (v < 0.0f || u + v > 1.0f)
        {
            return false;
        }

        float candidateDistance = edge2.Dot(q) * inverseDeterminant;
        if (candidateDistance < Epsilon)
        {
            return false;
        }

        distance = candidateDistance;
        return true;
    }

    private static Vector3 ToGodotVector3(NumericsVector3 vector) =>
        new(vector.X, vector.Y, vector.Z);

    private static Vector3 GetCornerPosition(SpatialMesh mesh, FaceCornerHandle corner) =>
        ToGodotVector3(mesh.GetVertexPosition(mesh.GetHalfEdge(corner).Origin));
}
