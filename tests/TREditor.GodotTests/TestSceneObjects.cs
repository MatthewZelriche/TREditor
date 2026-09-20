using Godot;
using TRMesh.Mesh;
using NumericsVector3 = System.Numerics.Vector3;

namespace TREditor.GodotTests;

// Shared scene content for tests that used to rely on Application's deprecated debug cube/cylinder seed.
internal static class TestSceneObjects
{
    public static readonly Transform3D CubeTransform = new(
        Basis.Identity,
        new Vector3(-1.5f, 0.0f, 0.0f)
    );

    public static readonly Transform3D CylinderTransform = new(
        Basis.Identity,
        new Vector3(1.5f, 0.0f, 0.0f)
    );

    public static (MeshObjectId CubeId, MeshObjectId CylinderId) AddCubeAndCylinder(
        Document document
    )
    {
        MeshObject cube = document.Add(
            "Cube",
            CubeTransform,
            new GDMesh(
                SpatialMeshBuilder.CreateAabb(
                    new NumericsVector3(-1.0f, -1.0f, -1.0f),
                    new NumericsVector3(2.0f, 2.0f, 2.0f)
                )
            )
        );
        MeshObject cylinder = document.Add(
            "Cylinder",
            CylinderTransform,
            new GDMesh(SpatialMeshBuilder.CreateCylinder(2.5f, 1.75f, 16))
        );
        return (cube.Id, cylinder.Id);
    }

    public static MeshObjectId AddCube(Document document)
    {
        return document
            .Add(
                "Cube",
                CubeTransform,
                new GDMesh(
                    SpatialMeshBuilder.CreateAabb(
                        new NumericsVector3(-1.0f, -1.0f, -1.0f),
                        new NumericsVector3(2.0f, 2.0f, 2.0f)
                    )
                )
            )
            .Id;
    }
}
