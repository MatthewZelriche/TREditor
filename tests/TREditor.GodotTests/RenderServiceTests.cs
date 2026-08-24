using System;
using GdUnit4;
using Godot;
using TRMesh.Mesh;
using static GdUnit4.Assertions;
using NumericsVector3 = System.Numerics.Vector3;

namespace TREditor.GodotTests;

[TestSuite]
public class RenderServiceTests
{
    [TestCase]
    [RequireGodotRuntime]
    public void RenderServiceKeepsOneRenderRootWhenTheSceneChanges()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var firstScene = new Scene();
        using var secondScene = new Scene();
        firstScene.Add(
            "First",
            Transform3D.Identity,
            new GDMesh(
                SpatialMeshBuilder.CreateAabb(NumericsVector3.Zero, NumericsVector3.One)
            )
        );
        secondScene.Add(
            "Second",
            Transform3D.Identity,
            new GDMesh(SpatialMeshBuilder.CreateCylinder(1.0f, 1.0f, 8))
        );
        var renderService = new RenderService(firstScene, mapRoot);
        var services = new SceneServices(firstScene, renderService);

        Node3D originalRenderRoot = mapRoot.GetNode<Node3D>("Render");
        MeshInstance3D firstInstance = originalRenderRoot.GetNode<MeshInstance3D>(
            "MeshObject_0"
        );
        if (firstInstance.Mesh is not ArrayMesh firstArrayMesh)
            throw new InvalidOperationException("Render entry did not create an ArrayMesh.");

        services.SetScene(secondScene);

        AssertThat(mapRoot.GetChildCount()).IsEqual(1);
        AssertThat(mapRoot.GetNode<Node3D>("Render")).IsSame(originalRenderRoot);
        AssertThat(originalRenderRoot.GetChildCount()).IsEqual(1);
        AssertBool(GodotObject.IsInstanceValid(firstArrayMesh)).IsFalse();

        services.Dispose();
        AssertBool(mapRoot.HasNode("Render")).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void RenderServiceTracksSceneObjectsAndBatchesGeometryRebuilds()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var scene = new Scene();
        var renderService = new RenderService(scene, mapRoot);
        var services = new SceneServices(scene, renderService);
        var initialTransform = new Transform3D(
            Basis.Identity,
            new Vector3(1.0f, 2.0f, 3.0f)
        );
        var mesh = new GDMesh(
            SpatialMeshBuilder.CreateAabb(NumericsVector3.Zero, NumericsVector3.One)
        );
        Scene.MeshObject meshObject = scene.Add("Cube", initialTransform, mesh);

        try
        {
            Node3D renderRoot = mapRoot.GetNode<Node3D>("Render");
            MeshInstance3D instance = renderRoot.GetNode<MeshInstance3D>(
                $"MeshObject_{meshObject.Id.Value}"
            );
            if (instance.Mesh is not ArrayMesh arrayMesh)
                throw new InvalidOperationException("Render entry did not create an ArrayMesh.");

            AssertThat(renderRoot.GetChildCount()).IsEqual(1);
            AssertThat(instance.Transform).IsEqual(initialTransform);
            AssertBool(mesh.Dirty).IsTrue();

            services.Flush();

            AssertBool(mesh.Dirty).IsFalse();
            AssertThat(arrayMesh.GetSurfaceCount()).IsEqual(1);
            AssertThat(mesh.Surface.ElementIndices.Count).IsEqual(36);

            scene.MarkGeometryChanged(meshObject.Id);
            scene.MarkGeometryChanged(meshObject.Id);
            AssertBool(mesh.Dirty).IsTrue();

            services.Flush();
            AssertBool(mesh.Dirty).IsFalse();

            var updatedTransform = new Transform3D(
                Basis.Identity,
                new Vector3(-3.0f, 4.0f, 5.0f)
            );
            scene.SetTransform(meshObject.Id, updatedTransform);
            AssertThat(instance.Transform).IsEqual(updatedTransform);

            scene.Remove(meshObject.Id);
            AssertThat(renderRoot.GetChildCount()).IsEqual(0);
            AssertBool(GodotObject.IsInstanceValid(arrayMesh)).IsFalse();
        }
        finally
        {
            services.Dispose();
        }

        AssertBool(mapRoot.HasNode("Render")).IsFalse();
    }
}
