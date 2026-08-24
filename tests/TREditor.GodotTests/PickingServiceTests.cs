using System.Threading.Tasks;
using GdUnit4;
using Godot;
using TRMesh.Mesh;
using static GdUnit4.Assertions;
using NumericsVector3 = System.Numerics.Vector3;

namespace TREditor.GodotTests;

[TestSuite]
public class PickingServiceTests
{
    [TestCase]
    [RequireGodotRuntime]
    public async Task PickingServiceRaycastMapsCollidersAndTracksSceneChanges()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        Node3D mapRoot = AutoFree(new Node3D());
        tree.Root.AddChild(mapRoot);
        using var scene = new Scene();
        Scene.MeshObject meshObject = scene.Add(
            "Cube",
            Transform3D.Identity,
            new GDMesh(
                SpatialMeshBuilder.CreateAabb(
                    new NumericsVector3(-1.0f, -1.0f, -1.0f),
                    new NumericsVector3(2.0f, 2.0f, 2.0f)
                )
            )
        );
        var pickingService = new PickingService(scene, mapRoot);
        var services = new SceneServices(scene, pickingService);

        try
        {
            services.Flush();
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);

            using PhysicsRayQueryParameters3D originalRay =
                PhysicsRayQueryParameters3D.Create(
                    new Vector3(0.0f, 0.0f, 3.0f),
                    new Vector3(0.0f, 0.0f, -3.0f)
                );
            AssertThat(pickingService.Raycast(originalRay)?.Value ?? -1)
                .IsEqual(meshObject.Id.Value);

            scene.SetTransform(
                meshObject.Id,
                new Transform3D(Basis.Identity, new Vector3(4.0f, 0.0f, 0.0f))
            );
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);

            AssertBool(pickingService.Raycast(originalRay) is null).IsTrue();
            using PhysicsRayQueryParameters3D movedRay =
                PhysicsRayQueryParameters3D.Create(
                    new Vector3(4.0f, 0.0f, 3.0f),
                    new Vector3(4.0f, 0.0f, -3.0f)
                );
            AssertThat(pickingService.Raycast(movedRay)?.Value ?? -1)
                .IsEqual(meshObject.Id.Value);

            scene.Remove(meshObject.Id);
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertBool(pickingService.Raycast(movedRay) is null).IsTrue();
        }
        finally
        {
            services.Dispose();
        }

        AssertBool(mapRoot.HasNode("Picking")).IsFalse();
    }
}
