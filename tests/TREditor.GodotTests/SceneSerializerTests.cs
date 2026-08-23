using System.Linq;
using GdUnit4;
using Godot;
using TRMesh.Mesh;
using static GdUnit4.Assertions;
using NumericsVector3 = System.Numerics.Vector3;

namespace TREditor.GodotTests;

[TestSuite]
public class SceneSerializerTests
{
    private const string ScenePath = "user://scene_serializer_round_trip.res";

    [TestCase]
    [RequireGodotRuntime]
    public void SaveAndLoadPreservesSceneObjectsAndMeshTopology()
    {
        RemoveSceneFile();
        try
        {
            var cubeTransform = new Transform3D(Basis.Identity, new Vector3(1, 2, 3));
            var cylinderTransform = new Transform3D(Basis.Identity, new Vector3(-4, 5, -6));

            using var source = new Scene();
            source.Add(
                "Cube",
                cubeTransform,
                new GDMesh(SpatialMeshBuilder.CreateAabb(NumericsVector3.Zero, NumericsVector3.One))
            );
            source.Add(
                "Cylinder",
                cylinderTransform,
                new GDMesh(SpatialMeshBuilder.CreateCylinder(2f, 1f, 8))
            );

            SceneSerializer.Save(source, ScenePath);
            using Scene loaded = SceneSerializer.Load(ScenePath);

            AssertThat(loaded.Objects.Count).IsEqual(2);

            Scene.MeshObject cube = loaded.Objects.Values.Single(value => value.Name == "Cube");
            AssertThat(cube.Transform).IsEqual(cubeTransform);
            AssertThat(cube.Mesh.SpatialMesh.VertexCount).IsEqual(8);
            AssertThat(cube.Mesh.SpatialMesh.FaceCount).IsEqual(6);

            Scene.MeshObject cylinder = loaded.Objects.Values.Single(value => value.Name == "Cylinder");
            AssertThat(cylinder.Transform).IsEqual(cylinderTransform);
            AssertThat(cylinder.Mesh.SpatialMesh.VertexCount).IsEqual(16);
            AssertThat(cylinder.Mesh.SpatialMesh.FaceCount).IsEqual(10);
        }
        finally
        {
            RemoveSceneFile();
        }
    }

    private static void RemoveSceneFile()
    {
        if (Godot.FileAccess.FileExists(ScenePath))
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(ScenePath));
    }
}
