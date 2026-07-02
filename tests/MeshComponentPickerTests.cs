using Godot;
using TREditorSharp;
using NumericsVector3 = System.Numerics.Vector3;

namespace TREditor2026.Tests;

public sealed class MeshComponentPickerTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );

    [Fact]
    public void TryPickComponents_FaceHitCarriesObjectIdWithoutNode()
    {
        SpatialMesh mesh = BuildQuadOnZPlane();
        Vector3 rayOrigin = new(0.5f, 0.5f, -1f);
        Vector3 rayDirection = Vector3.Back;

        bool picked = MeshComponentPicker.TryPickComponents(
            ObjectId,
            mesh,
            rayOrigin,
            rayDirection,
            maxDistance: 10f,
            vertexRadius: 0.01f,
            edgeRadius: 0.01f,
            out ScenePickHit vertex,
            out ScenePickHit edge,
            out ScenePickHit face
        );

        Assert.True(picked);
        Assert.False(vertex.HasHit);
        Assert.False(edge.HasHit);
        Assert.True(face.HasHit);
        Assert.Equal(ObjectId, face.ObjectId);
        Assert.Equal(ScenePickElementKind.Face, face.Kind);
    }

    [Fact]
    public void ModelGlobalTransform_ConvertsWorldRayBeforePicking()
    {
        SpatialMesh mesh = BuildQuadOnZPlane();
        Transform3D globalTransform = new(Basis.Identity, new Vector3(10f, 0f, 0f));

        Vector3 worldOrigin = new(10.5f, 0.5f, -1f);
        Vector3 worldDirection = Vector3.Back;

        Transform3D inverse = globalTransform.AffineInverse();
        Vector3 localOrigin = inverse * worldOrigin;
        Vector3 localDirection = (inverse.Basis * worldDirection).Normalized();

        Assert.True(
            MeshComponentPicker.TryPickComponents(
                ObjectId,
                mesh,
                localOrigin,
                localDirection,
                maxDistance: 10f,
                vertexRadius: 0.01f,
                edgeRadius: 0.01f,
                out _,
                out _,
                out ScenePickHit face
            )
        );
        Assert.Equal(ObjectId, face.ObjectId);

        Vector3 staleLocalOrigin = worldOrigin;
        Vector3 staleLocalDirection = worldDirection;
        Assert.False(
            MeshComponentPicker.TryPickComponents(
                ObjectId,
                mesh,
                staleLocalOrigin,
                staleLocalDirection,
                maxDistance: 10f,
                vertexRadius: 0.01f,
                edgeRadius: 0.01f,
                out _,
                out _,
                out ScenePickHit staleFace
            )
        );
        Assert.False(staleFace.HasHit);
    }

    private static SpatialMesh BuildQuadOnZPlane()
    {
        SpatialMesh mesh = new();
        mesh.AddFace(
            [
                mesh.AddVertex(new NumericsVector3(0f, 0f, 0f)),
                mesh.AddVertex(new NumericsVector3(1f, 0f, 0f)),
                mesh.AddVertex(new NumericsVector3(1f, 1f, 0f)),
                mesh.AddVertex(new NumericsVector3(0f, 1f, 0f)),
            ]
        );
        return mesh;
    }
}
