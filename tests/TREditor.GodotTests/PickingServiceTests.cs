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
    public async Task PickingServiceRaycastMapsCollidersAndTracksDocumentChanges()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        Node3D mapRoot = AutoFree(new Node3D());
        tree.Root.AddChild(mapRoot);
        using var document = new Document();
        MeshObject meshObject = document.Add(
            "Cube",
            Transform3D.Identity,
            new GDMesh(
                SpatialMeshBuilder.CreateAabb(
                    new NumericsVector3(-1.0f, -1.0f, -1.0f),
                    new NumericsVector3(2.0f, 2.0f, 2.0f)
                )
            )
        );
        var previews = new PreviewState();
        var pickingService = new PickingService(document, mapRoot, previews);
        var services = new SceneServices(document, previews, pickingService);

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

            AssertBool(
                document.ReplaceObject(
                    meshObject,
                    meshObject with
                    {
                        Transform = new Transform3D(Basis.Identity, new Vector3(4.0f, 0.0f, 0.0f)),
                    }
                )
            ).IsTrue();
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);

            AssertBool(pickingService.Raycast(originalRay) is null).IsTrue();
            using PhysicsRayQueryParameters3D movedRay =
                PhysicsRayQueryParameters3D.Create(
                    new Vector3(4.0f, 0.0f, 3.0f),
                    new Vector3(4.0f, 0.0f, -3.0f)
                );
            AssertThat(pickingService.Raycast(movedRay)?.Value ?? -1)
                .IsEqual(meshObject.Id.Value);

            document.Remove(meshObject.Id)?.Mesh?.Dispose();
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            AssertBool(pickingService.Raycast(movedRay) is null).IsTrue();
        }
        finally
        {
            services.Dispose();
        }

        AssertBool(mapRoot.HasNode("Picking")).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task PickingServiceFollowsPreviewTransforms()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        Node3D mapRoot = AutoFree(new Node3D());
        tree.Root.AddChild(mapRoot);
        using var document = new Document();
        MeshObject meshObject = document.Add(
            "Cube",
            Transform3D.Identity,
            new GDMesh(
                SpatialMeshBuilder.CreateAabb(
                    new NumericsVector3(-1.0f, -1.0f, -1.0f),
                    new NumericsVector3(2.0f, 2.0f, 2.0f)
                )
            )
        );
        var previews = new PreviewState();
        var pickingService = new PickingService(document, mapRoot, previews);
        var services = new SceneServices(document, previews, pickingService);

        try
        {
            services.Flush();
            using PhysicsRayQueryParameters3D originalRay =
                PhysicsRayQueryParameters3D.Create(
                    new Vector3(0.0f, 0.0f, 3.0f),
                    new Vector3(0.0f, 0.0f, -3.0f)
                );
            using PhysicsRayQueryParameters3D previewRay =
                PhysicsRayQueryParameters3D.Create(
                    new Vector3(4.0f, 0.0f, 3.0f),
                    new Vector3(4.0f, 0.0f, -3.0f)
                );

            previews.SetTransform(
                meshObject.Id,
                new Transform3D(Basis.Identity, new Vector3(4.0f, 0.0f, 0.0f))
            );
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);

            AssertThat(pickingService.Raycast(previewRay)?.Value ?? -1)
                .IsEqual(meshObject.Id.Value);
            AssertBool(pickingService.Raycast(originalRay) is null).IsTrue();

            previews.ClearTransform(meshObject.Id);
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);

            AssertThat(pickingService.Raycast(originalRay)?.Value ?? -1)
                .IsEqual(meshObject.Id.Value);
            AssertBool(pickingService.Raycast(previewRay) is null).IsTrue();
        }
        finally
        {
            services.Dispose();
        }
    }
}
