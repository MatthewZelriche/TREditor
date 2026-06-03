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
        int aIndex,
        int bIndex,
        int cIndex
    )
    {
        ArgumentNullException.ThrowIfNull(sourceMesh);

        faces.Add(ToGodotVector3(sourceMesh.GetVertexPositionByDenseIndex(aIndex)));
        faces.Add(ToGodotVector3(sourceMesh.GetVertexPositionByDenseIndex(bIndex)));
        faces.Add(ToGodotVector3(sourceMesh.GetVertexPositionByDenseIndex(cIndex)));
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

        var shape = new ConcavePolygonShape3D();
        shape.SetFaces(faces.ToArray());
        _shapeNode.Shape = shape;
    }

    private static Vector3 ToGodotVector3(NumericsVector3 vector) =>
        new(vector.X, vector.Y, vector.Z);

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
