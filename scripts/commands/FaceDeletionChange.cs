#nullable enable

using System;
using System.Collections.Generic;
using TREditorSharp;

/// <summary>
/// Captured state required to repeatedly delete and restore one polygon face.
/// </summary>
public sealed class FaceDeletionChange
{
    private readonly VertexHandle[] _vertices;
    private readonly FaceTextureState _textureState;

    public EditorObjectId ObjectId { get; }
    public FaceHandle OriginalFace { get; }
    public FaceHandle Face { get; private set; }

    public SelectionTarget SelectionTarget => SelectionTarget.ForFace(ObjectId, Face);

    private FaceDeletionChange(
        EditorObjectId objectId,
        FaceHandle face,
        VertexHandle[] vertices,
        FaceTextureState textureState
    )
    {
        ObjectId = objectId;
        OriginalFace = face;
        Face = face;
        _vertices = vertices;
        _textureState = textureState;
    }

    public static FaceDeletionChange? Capture(
        EditorObjectId objectId,
        SpatialMesh mesh,
        FaceHandle face
    )
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (!IsLiveFace(mesh, face))
            return null;

        List<VertexHandle> vertices = [];
        foreach (FaceCornerHandle corner in mesh.HalfEdgesAroundFace(face))
            vertices.Add(mesh.GetHalfEdge(corner).Origin);

        return new FaceDeletionChange(
            objectId,
            face,
            vertices.ToArray(),
            FaceTextureState.Capture(mesh, face)
        );
    }

    public bool Delete(SpatialMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        return mesh.RemoveFace(Face);
    }

    public void Restore(SpatialMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        Face = mesh.AddFace(_vertices);
        _textureState.Apply(mesh, Face);
    }

    private static bool IsLiveFace(SpatialMesh mesh, FaceHandle face)
    {
        foreach (FaceHandle liveFace in mesh.EnumerateLiveFaces())
        {
            if (liveFace == face)
                return true;
        }

        return false;
    }
}
