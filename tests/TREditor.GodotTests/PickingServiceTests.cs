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
    private static readonly Vector2 ViewportSize = new(640.0f, 480.0f);

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
        var services = new SceneServices(
            document,
            previews,
            new SelectionState(),
            new SelectionModeState(),
            pickingService
        );

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
    public async Task PickingServiceRebuildsCollidersOnGeometryChanged()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        Node3D mapRoot = AutoFree(new Node3D());
        tree.Root.AddChild(mapRoot);
        using var document = new Document();
        var mesh = new GDMesh(
            SpatialMeshBuilder.CreateAabb(
                new NumericsVector3(-1.0f, -1.0f, -1.0f),
                new NumericsVector3(2.0f, 2.0f, 2.0f)
            )
        );
        MeshObject meshObject = document.Add("Cube", Transform3D.Identity, mesh);
        var previews = new PreviewState();
        var pickingService = new PickingService(document, mapRoot, previews);
        var services = new SceneServices(
            document,
            previews,
            new SelectionState(),
            new SelectionModeState(),
            pickingService
        );

        try
        {
            services.Flush();
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);

            using PhysicsRayQueryParameters3D originalRay =
                PhysicsRayQueryParameters3D.Create(
                    new Vector3(0.0f, 0.0f, 3.0f),
                    new Vector3(0.0f, 0.0f, -3.0f)
                );
            using PhysicsRayQueryParameters3D relocatedRay =
                PhysicsRayQueryParameters3D.Create(
                    new Vector3(4.0f, 0.0f, 3.0f),
                    new Vector3(4.0f, 0.0f, -3.0f)
                );

            AssertThat(pickingService.Raycast(originalRay)?.Value ?? -1)
                .IsEqual(meshObject.Id.Value);

            var relocated = new GDMesh(
                SpatialMeshBuilder.CreateAabb(
                    new NumericsVector3(3.0f, -1.0f, -1.0f),
                    new NumericsVector3(2.0f, 2.0f, 2.0f)
                )
            );
            mesh.MeshData = relocated.MeshData;
            relocated.Dispose();
            AssertBool(document.CommitGeometryChange(meshObject.Id)).IsTrue();
            services.Flush();
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);

            AssertBool(pickingService.Raycast(originalRay) is null).IsTrue();
            AssertThat(pickingService.Raycast(relocatedRay)?.Value ?? -1)
                .IsEqual(meshObject.Id.Value);
        }
        finally
        {
            services.Dispose();
        }
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
        var services = new SceneServices(
            document,
            previews,
            new SelectionState(),
            new SelectionModeState(),
            pickingService
        );

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

    [TestCase]
    [RequireGodotRuntime]
    public void ComponentPickingHonorsMasksAndFreeModePriority()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        Node3D mapRoot = AutoFree(new Node3D());
        tree.Root.AddChild(mapRoot);
        MainViewport viewport = CreateViewport();
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
        using var services = new SceneServices(
            document,
            previews,
            new SelectionState(),
            new SelectionModeState(),
            pickingService
        );
        services.Flush();

        Vector2 vertexScreen = viewport.Camera.UnprojectPosition(new Vector3(1.0f, 1.0f, 1.0f));
        ComponentPickingHit? vertex = pickingService.PickComponent(
            viewport.Camera,
            vertexScreen,
            ComponentMask.All
        );
        AssertBool(vertex.HasValue).IsTrue();
        AssertThat(vertex!.Value.Component.ObjectId).IsEqual(meshObject.Id);
        AssertThat(vertex.Value.Component.Type).IsEqual(MeshComponentType.Vertex);

        Vector2 edgeScreen = viewport.Camera.UnprojectPosition(new Vector3(1.0f, 0.0f, 1.0f));
        ComponentPickingHit? edge = pickingService.PickComponent(
            viewport.Camera,
            edgeScreen,
            ComponentMask.All
        );
        AssertBool(edge.HasValue).IsTrue();
        AssertThat(edge!.Value.Component.Type).IsEqual(MeshComponentType.Edge);

        ComponentPickingHit? edgeOnly = pickingService.PickComponent(
            viewport.Camera,
            edgeScreen,
            ComponentMask.Edge
        );
        AssertBool(edgeOnly.HasValue).IsTrue();
        AssertThat(edgeOnly!.Value.Component.Type).IsEqual(MeshComponentType.Edge);

        ComponentPickingHit? face = pickingService.PickComponent(
            viewport.Camera,
            new Vector2(ViewportSize.X * 0.5f, ViewportSize.Y * 0.5f),
            ComponentMask.Face
        );
        AssertBool(face.HasValue).IsTrue();
        AssertThat(face!.Value.Component.Type).IsEqual(MeshComponentType.Face);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ComponentPickingPadsVerticesAndRejectsOccludedComponents()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        Node3D mapRoot = AutoFree(new Node3D());
        tree.Root.AddChild(mapRoot);
        MainViewport viewport = CreateViewport();
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
        using var services = new SceneServices(
            document,
            previews,
            new SelectionState(),
            new SelectionModeState(),
            pickingService
        );
        services.Flush();

        Vector2 frontVertex = viewport.Camera.UnprojectPosition(new Vector3(1.0f, 1.0f, 1.0f));
        ComponentPickingHit? padded = pickingService.PickComponent(
            viewport.Camera,
            frontVertex + new Vector2(5.0f, 0.0f),
            ComponentMask.Vertex
        );
        AssertBool(padded.HasValue).IsTrue();
        AssertThat(padded!.Value.Component.ObjectId).IsEqual(meshObject.Id);

        Vector2 backVertex = viewport.Camera.UnprojectPosition(new Vector3(1.0f, 1.0f, -1.0f));
        ComponentPickingHit? hidden = pickingService.PickComponent(
            viewport.Camera,
            backVertex,
            ComponentMask.Vertex,
            1.0f
        );
        AssertBool(hidden.HasValue).IsFalse();
    }

    private static MainViewport CreateViewport()
    {
        MainViewport viewport = AutoFree(
            new MainViewport { Position = Vector2.Zero, Size = ViewportSize }
        );
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(viewport);
        viewport.Camera.LookAtFromPosition(new Vector3(0.0f, 0.0f, 5.0f), Vector3.Zero, Vector3.Up);
        return viewport;
    }
}
