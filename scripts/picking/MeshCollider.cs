using System;
using System.Collections.Generic;
using Godot;
using TREditorSharp;
using NumericsVector3 = System.Numerics.Vector3;

public partial class MeshCollider : StaticBody3D
{
    private CollisionShape3D _shapeNode;

    /// <summary>
    /// Appends one collision triangle into a caller-owned rebuild scratch list
    /// (e.g. <see cref="TRMeshGD"/> scratch buffers). Does not update this node.
    /// </summary>
    public static void AppendRebuildTriangle(
        SpatialMesh sourceMesh,
        List<Vector3> faces,
        FaceCornerHandle aCorner,
        FaceCornerHandle bCorner,
        FaceCornerHandle cCorner
    )
    {
        ArgumentNullException.ThrowIfNull(sourceMesh);

        faces.Add(GetCornerPosition(sourceMesh, aCorner));
        faces.Add(GetCornerPosition(sourceMesh, bCorner));
        faces.Add(GetCornerPosition(sourceMesh, cCorner));
    }

    /// <summary>
    /// Commits a filled rebuild scratch face list to this node's collision shape.
    /// </summary>
    public void Rebuild(List<Vector3> faces)
    {
        EnsureShapeNode();

        if (faces.Count == 0)
        {
            _shapeNode.Shape = null;
            return;
        }

        var shape = new ConcavePolygonShape3D
        {
            // Enable backface collision - easy fix for our Trimesh colliders having a different
            // winding order than Godot's default. We should probably revisit this to properly
            // handle winding order on the Trimesh colliders in the future.
            BackfaceCollision = true,
        };
        shape.SetFaces(faces.ToArray());
        _shapeNode.Shape = shape;
    }

    private static Vector3 ToGodotVector3(NumericsVector3 vector) =>
        new(vector.X, vector.Y, vector.Z);

    private static Vector3 GetCornerPosition(SpatialMesh mesh, FaceCornerHandle corner) =>
        ToGodotVector3(mesh.GetVertexPosition(mesh.GetHalfEdge(corner).Origin));

    private void EnsureShapeNode()
    {
        if (_shapeNode != null)
        {
            return;
        }

        _shapeNode =
            GetNodeOrNull<CollisionShape3D>("CollisionShape3D")
            ?? new CollisionShape3D { Name = "CollisionShape3D" };
        if (_shapeNode.GetParent() != this)
        {
            AddChild(_shapeNode);
        }
    }
}
