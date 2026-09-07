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
    public void RenderServiceKeepsOneRenderRootWhenTheDocumentChanges()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var firstDocument = new Document();
        using var secondDocument = new Document();
        firstDocument.Add(
            "First",
            Transform3D.Identity,
            new GDMesh(
                SpatialMeshBuilder.CreateAabb(NumericsVector3.Zero, NumericsVector3.One)
            )
        );
        secondDocument.Add(
            "Second",
            Transform3D.Identity,
            new GDMesh(SpatialMeshBuilder.CreateCylinder(1.0f, 1.0f, 8))
        );
        var previews = new PreviewState();
        var renderService = new RenderService(firstDocument, mapRoot, previews);
        var services = new SceneServices(firstDocument, previews, renderService);

        Node3D originalRenderRoot = mapRoot.GetNode<Node3D>("Render");
        MeshInstance3D firstInstance = originalRenderRoot.GetNode<MeshInstance3D>(
            "MeshObject_0"
        );
        if (firstInstance.Mesh is not ArrayMesh firstArrayMesh)
            throw new InvalidOperationException("Render entry did not create an ArrayMesh.");

        services.SetDocument(secondDocument);

        AssertThat(mapRoot.GetChildCount()).IsEqual(1);
        AssertThat(mapRoot.GetNode<Node3D>("Render")).IsSame(originalRenderRoot);
        AssertThat(originalRenderRoot.GetChildCount()).IsEqual(1);
        AssertBool(GodotObject.IsInstanceValid(firstArrayMesh)).IsFalse();

        services.Dispose();
        AssertBool(mapRoot.HasNode("Render")).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void RenderServiceShowsPreviewsUntilTheyAreCleared()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var document = new Document();
        using var replacementDocument = new Document();
        var previews = new PreviewState();
        var renderService = new RenderService(document, mapRoot, previews);
        var services = new SceneServices(document, previews, renderService);
        var initialTransform = new Transform3D(Basis.Identity, new Vector3(1.0f, 0.0f, 0.0f));
        MeshObject meshObject = document.Add(
            "Cube",
            initialTransform,
            new GDMesh(
                SpatialMeshBuilder.CreateAabb(NumericsVector3.Zero, NumericsVector3.One)
            )
        );

        try
        {
            MeshInstance3D instance = mapRoot
                .GetNode<Node3D>("Render")
                .GetNode<MeshInstance3D>($"MeshObject_{meshObject.Id.Value}");
            AssertThat(instance.Transform).IsEqual(initialTransform);

            var firstPreview = new Transform3D(Basis.Identity, new Vector3(2.0f, 0.0f, 0.0f));
            previews.SetTransform(meshObject.Id, firstPreview);
            AssertThat(instance.Transform).IsEqual(firstPreview);

            var secondPreview = new Transform3D(Basis.Identity, new Vector3(3.0f, 0.0f, 0.0f));
            previews.SetTransform(meshObject.Id, secondPreview);
            AssertThat(instance.Transform).IsEqual(secondPreview);

            // A command landing mid-drag updates the document without disturbing the preview.
            var committedTransform = new Transform3D(
                Basis.Identity,
                new Vector3(0.0f, 5.0f, 0.0f)
            );
            AssertBool(
                document.ReplaceObject(
                    meshObject,
                    meshObject with { Transform = committedTransform }
                )
            ).IsTrue();
            AssertThat(instance.Transform).IsEqual(secondPreview);

            previews.ClearTransform(meshObject.Id);
            AssertThat(instance.Transform).IsEqual(committedTransform);

            previews.SetTransform(meshObject.Id, firstPreview);
            services.SetDocument(replacementDocument);
            AssertBool(previews.TryGetTransform(meshObject.Id, out _)).IsFalse();
        }
        finally
        {
            services.Dispose();
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void RenderServiceTracksMeshObjectsAndBatchesGeometryRebuilds()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var document = new Document();
        var previews = new PreviewState();
        var renderService = new RenderService(document, mapRoot, previews);
        var services = new SceneServices(document, previews, renderService);
        var initialTransform = new Transform3D(
            Basis.Identity,
            new Vector3(1.0f, 2.0f, 3.0f)
        );
        var mesh = new GDMesh(
            SpatialMeshBuilder.CreateAabb(NumericsVector3.Zero, NumericsVector3.One)
        );
        MeshObject meshObject = document.Add("Cube", initialTransform, mesh);

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

            document.CommitGeometryChange(meshObject.Id);
            document.CommitGeometryChange(meshObject.Id);
            AssertBool(mesh.Dirty).IsTrue();

            services.Flush();
            AssertBool(mesh.Dirty).IsFalse();

            var updatedTransform = new Transform3D(
                Basis.Identity,
                new Vector3(-3.0f, 4.0f, 5.0f)
            );
            AssertBool(
                document.ReplaceObject(
                    meshObject,
                    meshObject with { Transform = updatedTransform }
                )
            ).IsTrue();
            AssertThat(instance.Transform).IsEqual(updatedTransform);

            document.Remove(meshObject.Id)?.Mesh?.Dispose();
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
