using System;
using Godot;
using TREditorSharp;
using NumericsVector2 = System.Numerics.Vector2;
using NumericsVector3 = System.Numerics.Vector3;

/// <summary>
/// Pure conversion from triangulated polygon face corners to render-surface data.
/// </summary>
public static class MeshRenderDataBuilder
{
    /// <summary>
    /// Returns the render surface belonging to <paramref name="face"/>'s material slot. Call this
    /// once per polygon face, then append every generated triangle into the returned surface.
    /// </summary>
    public static MeshRenderData GetFaceSurface(
        SpatialMesh sourceMesh,
        MeshRenderSurfaceSet output,
        FaceHandle face
    )
    {
        ArgumentNullException.ThrowIfNull(sourceMesh);
        ArgumentNullException.ThrowIfNull(output);

        return output.GetOrCreateSurface(sourceMesh.GetFaceMaterialSlot(face));
    }

    /// <summary>
    /// Appends one render triangle using each supplied face corner's geometric position and UV.
    /// The caller controls winding by choosing the corner order.
    /// </summary>
    public static void AppendTriangle(
        SpatialMesh sourceMesh,
        MeshRenderData output,
        FaceCornerHandle aCorner,
        FaceCornerHandle bCorner,
        FaceCornerHandle cCorner
    )
    {
        ArgumentNullException.ThrowIfNull(sourceMesh);
        ArgumentNullException.ThrowIfNull(output);

        Vector3 a = GetCornerPosition(sourceMesh, aCorner);
        Vector3 b = GetCornerPosition(sourceMesh, bCorner);
        Vector3 c = GetCornerPosition(sourceMesh, cCorner);
        Vector3 normal = CalculateTriangleNormal(a, b, c);
        int firstRenderIndex = output.Vertices.Count;

        // Emit one render vertex per triangle corner rather than welding by geometric vertex.
        // Multiple polygon face corners may share the same position while owning different UVs,
        // and collapsing them here would erase that texture boundary.
        output.Vertices.Add(a);
        output.Vertices.Add(b);
        output.Vertices.Add(c);
        output.Normals.Add(normal);
        output.Normals.Add(normal);
        output.Normals.Add(normal);
        output.Uvs.Add(ToGodotVector2(sourceMesh.GetFaceCornerUv(aCorner)));
        output.Uvs.Add(ToGodotVector2(sourceMesh.GetFaceCornerUv(bCorner)));
        output.Uvs.Add(ToGodotVector2(sourceMesh.GetFaceCornerUv(cCorner)));
        output.Indices.Add(firstRenderIndex);
        output.Indices.Add(firstRenderIndex + 1);
        output.Indices.Add(firstRenderIndex + 2);
    }

    private static Vector3 GetCornerPosition(SpatialMesh mesh, FaceCornerHandle corner) =>
        ToGodotVector3(mesh.GetVertexPosition(mesh.GetHalfEdge(corner).Origin));

    private static Vector3 CalculateTriangleNormal(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 normal = (b - a).Cross(c - a);
        return normal.LengthSquared() > 0.0f ? normal.Normalized() : Vector3.Up;
    }

    private static Vector2 ToGodotVector2(NumericsVector2 vector) => new(vector.X, vector.Y);

    private static Vector3 ToGodotVector3(NumericsVector3 vector) =>
        new(vector.X, vector.Y, vector.Z);
}
