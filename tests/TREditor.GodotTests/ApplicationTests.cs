using System.Linq;
using GdUnit4;
using Godot;
using TRMesh.Mesh;
using static GdUnit4.Assertions;
using NumericsVector3 = System.Numerics.Vector3;

namespace TREditor.GodotTests;

[TestSuite]
public class ApplicationTests
{
    private const string ScenePath = "user://application_round_trip.res";

    [TestCase]
    [RequireGodotRuntime]
    public void ApplicationSavesAndOpensItsCurrentScene()
    {
        RemoveSceneFile();
        try
        {
            Node3D mapRoot = AutoFree(new Node3D());
            using var application = new Application(mapRoot);
            string scenePath = ProjectSettings.GlobalizePath(ScenePath);
            var cubeTransform = new Transform3D(Basis.Identity, new Vector3(1, 2, 3));
            var cylinderTransform = new Transform3D(Basis.Identity, new Vector3(-4, 5, -6));
            Scene source = application.CurrentScene!;
            AssertThat(source).IsNotNull();
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

            application.SaveSceneAs(scenePath);
            application.NewScene();
            AssertThat(application.CurrentScene?.Objects.Count ?? -1).IsEqual(0);
            AssertThat(application.CurrentFilePath).IsNull();

            application.OpenScene(scenePath);

            Scene loaded = application.CurrentScene!;
            AssertThat(loaded).IsNotNull();
            AssertThat(application.CurrentFilePath).IsEqual(scenePath);
            AssertThat(loaded.Objects.Count).IsEqual(2);

            Scene.MeshObject cube = loaded.Objects.Values.Single(value => value.Name == "Cube");
            AssertThat(cube.Transform).IsEqual(cubeTransform);
            AssertThat(cube.Mesh.SpatialMesh.VertexCount).IsEqual(8);
            AssertThat(cube.Mesh.SpatialMesh.FaceCount).IsEqual(6);

            Scene.MeshObject cylinder = loaded.Objects.Values.Single(value => value.Name == "Cylinder");
            AssertThat(cylinder.Transform).IsEqual(cylinderTransform);
            AssertThat(cylinder.Mesh.SpatialMesh.VertexCount).IsEqual(16);
            AssertThat(cylinder.Mesh.SpatialMesh.FaceCount).IsEqual(10);

            application.SaveScene();
            application.Update(0.0);
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
