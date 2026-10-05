using System.Linq;
using System.Threading.Tasks;
using GdUnit4;
using Godot;
using TRMesh.Mesh;
using static GdUnit4.Assertions;
using NumericsVector3 = System.Numerics.Vector3;

namespace TREditor.GodotTests;

[TestSuite]
public class ComponentSelectionTests
{
    [TestCase]
    [RequireGodotRuntime]
    public async Task ComponentModeClickSelectsAComponentAndSupportsHistory()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        Node3D mapRoot = AutoFree(new Node3D());
        tree.Root.AddChild(mapRoot);
        MainViewport viewport = AutoFree(
            new MainViewport { Position = Vector2.Zero, Size = new Vector2(640.0f, 480.0f) }
        );
        tree.Root.AddChild(viewport);
        viewport.Camera.LookAtFromPosition(new Vector3(0.0f, 0.0f, 5.0f), Vector3.Zero, Vector3.Up);

        using var application = new Application(mapRoot);
        MeshObject meshObject = application.CurrentDocument!.Add(
            "Cube",
            Transform3D.Identity,
            new GDMesh(
                SpatialMeshBuilder.CreateAabb(
                    new NumericsVector3(-1.0f, -1.0f, -1.0f),
                    new NumericsVector3(2.0f, 2.0f, 2.0f)
                )
            )
        );
        application.Update(0.0);
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

        application.SelectionMode.SelectAllComponents();
        Node3D componentRoot = mapRoot.GetNode<Node3D>("ComponentSelection");
        Node3D objectVisual = componentRoot.GetNode<Node3D>(
            $"MeshObject_{meshObject.Id.Value}"
        );
        MultiMeshInstance3D vertices = objectVisual.GetNode<MultiMeshInstance3D>("Vertices");
        MeshInstance3D edges = objectVisual.GetNode<MeshInstance3D>("Edges");
        AssertBool(vertices.Visible).IsTrue();
        AssertBool(edges.Visible).IsTrue();
        AssertThat(vertices.Multimesh.InstanceCount).IsEqual(8);
        var mutedVertexMaterial =
            (ShaderMaterial)((QuadMesh)vertices.Multimesh.Mesh).Material;
        Color mutedVertexColor = mutedVertexMaterial.GetShaderParameter("point_color").AsColor();
        AssertFloat(mutedVertexColor.B).IsGreater(mutedVertexColor.R);
        AssertThat(mutedVertexMaterial.RenderPriority)
            .IsEqual((int)Material.RenderPriorityMax - 2);
        AssertBool(
            mutedVertexMaterial.Shader.Code.Contains(
                "ALPHA = 1.0;",
                System.StringComparison.Ordinal
            )
        ).IsTrue();
        var mutedEdgeMaterial = (StandardMaterial3D)edges.MaterialOverride;
        AssertFloat(mutedEdgeMaterial.AlbedoColor.B)
            .IsGreater(mutedEdgeMaterial.AlbedoColor.R);
        AssertThat(mutedEdgeMaterial.Transparency)
            .IsEqual(BaseMaterial3D.TransparencyEnum.Alpha);
        AssertThat(mutedEdgeMaterial.RenderPriority)
            .IsEqual((int)Material.RenderPriorityMax - 5);

        Vector2 vertexScreen = viewport.Camera.UnprojectPosition(new Vector3(1.0f, 1.0f, 1.0f));
        application.HandleToolMouseMove(new EditorMouseInput(viewport.Camera, vertexScreen));
        MeshInstance3D hoveredVertex = componentRoot.GetNode<MeshInstance3D>("HoveredVertex");
        AssertBool(hoveredVertex.Visible).IsTrue();
        float hoveredVertexPixels = ((ShaderMaterial)hoveredVertex.MaterialOverride)
            .GetShaderParameter("point_size_pixels")
            .AsSingle();
        AssertFloat(hoveredVertexPixels).IsGreater(10.0f);
        AssertThat(hoveredVertex.MaterialOverride!.RenderPriority)
            .IsEqual((int)Material.RenderPriorityMax - 1);
        AssertStencilXRayPass((ShaderMaterial)hoveredVertex.MaterialOverride!, 1);

        viewport.Camera.LookAtFromPosition(
            new Vector3(0.0f, 0.0f, 20.0f),
            Vector3.Zero,
            Vector3.Up
        );
        Vector2 distantVertexScreen = viewport.Camera.UnprojectPosition(
            new Vector3(1.0f, 1.0f, 1.0f)
        );
        application.HandleToolMouseMove(
            new EditorMouseInput(viewport.Camera, distantVertexScreen)
        );
        AssertFloat(
            ((ShaderMaterial)hoveredVertex.MaterialOverride)
                .GetShaderParameter("point_size_pixels")
                .AsSingle()
        ).IsEqual(hoveredVertexPixels);
        viewport.Camera.LookAtFromPosition(
            new Vector3(0.0f, 0.0f, 5.0f),
            Vector3.Zero,
            Vector3.Up
        );

        application.HandleToolMouseDown(new EditorMouseInput(viewport.Camera, vertexScreen));

        AssertThat(application.Selection.Current.ObjectIds.Count).IsEqual(0);
        AssertThat(application.Selection.Current.Components.Count).IsEqual(1);
        MeshComponentId selected = application.Selection.Current.Components.Single();
        AssertThat(selected.ObjectId).IsEqual(meshObject.Id);
        AssertThat(selected.Type).IsEqual(MeshComponentType.Vertex);
        AssertThat(application.UndoDisplayName).IsEqual("Select Component");
        MeshInstance3D selectedVertex = objectVisual.GetNode<MeshInstance3D>("SelectedVertex");
        AssertBool(selectedVertex.Visible).IsTrue();
        Color selectedColor = ((ShaderMaterial)selectedVertex.MaterialOverride)
            .GetShaderParameter("point_color")
            .AsColor();
        AssertFloat(selectedColor.R).IsGreater(0.9f);
        AssertFloat(selectedColor.G).IsGreater(0.7f);
        AssertThat(selectedVertex.MaterialOverride!.RenderPriority)
            .IsEqual((int)Material.RenderPriorityMax);
        AssertBool(selectedVertex.IgnoreOcclusionCulling).IsTrue();
        AssertStencilXRayPass((ShaderMaterial)selectedVertex.MaterialOverride, 1);

        Vector2 edgeScreen = viewport.Camera.UnprojectPosition(new Vector3(1.0f, 0.0f, 1.0f));
        application.HandleToolMouseMove(new EditorMouseInput(viewport.Camera, edgeScreen));
        MeshInstance3D hoveredEdge = componentRoot.GetNode<MeshInstance3D>("HoveredEdge");
        AssertBool(hoveredEdge.Visible).IsTrue();
        AssertFloat(
            ((ShaderMaterial)hoveredEdge.MaterialOverride)
                .GetShaderParameter("line_width_pixels")
                .AsSingle()
        ).IsGreater(4.0f);
        AssertThat(hoveredEdge.MaterialOverride!.RenderPriority)
            .IsEqual((int)Material.RenderPriorityMax - 4);
        AssertBool(hoveredEdge.IgnoreOcclusionCulling).IsTrue();
        AssertBool(hoveredEdge.CustomAabb.HasPoint(new Vector3(1.0f, 0.0f, 1.0f))).IsTrue();
        AssertFloat(hoveredEdge.ExtraCullMargin).IsGreater(0.0f);
        AssertStencilXRayPass((ShaderMaterial)hoveredEdge.MaterialOverride, 1);
        application.HandleToolMouseDown(new EditorMouseInput(viewport.Camera, edgeScreen));
        AssertThat(application.Selection.Current.Components.Single().Type)
            .IsEqual(MeshComponentType.Edge);
        MeshInstance3D selectedEdge = objectVisual.GetNode<MeshInstance3D>("SelectedEdge");
        AssertBool(selectedEdge.Visible).IsTrue();
        AssertThat(selectedEdge.MaterialOverride!.RenderPriority)
            .IsEqual((int)Material.RenderPriorityMax - 3);
        AssertBool(selectedEdge.IgnoreOcclusionCulling).IsTrue();
        AssertBool(selectedEdge.CustomAabb.HasPoint(new Vector3(1.0f, 0.0f, 1.0f))).IsTrue();

        Vector2 faceScreen = viewport.Camera.UnprojectPosition(new Vector3(0.0f, 0.0f, 1.0f));
        application.HandleToolMouseMove(new EditorMouseInput(viewport.Camera, faceScreen));
        MeshInstance3D hoveredFace = componentRoot.GetNode<MeshInstance3D>("HoveredFace");
        AssertBool(hoveredFace.Visible).IsTrue();
        AssertThat(((ArrayMesh)hoveredFace.Mesh).GetSurfaceCount()).IsEqual(1);
        application.HandleToolMouseDown(new EditorMouseInput(viewport.Camera, faceScreen));
        AssertThat(application.Selection.Current.Components.Single().Type)
            .IsEqual(MeshComponentType.Face);
        MeshInstance3D selectedFace = objectVisual.GetNode<MeshInstance3D>("SelectedFace");
        AssertBool(selectedFace.Visible).IsTrue();
        Color selectedFaceColor = ((ShaderMaterial)selectedFace.MaterialOverride)
            .GetShaderParameter("overlay_color")
            .AsColor();
        AssertFloat(selectedFaceColor.A).IsGreater(0.4f);

        AssertBool(application.Undo()).IsTrue();
        AssertThat(application.Selection.Current.Components.Single().Type)
            .IsEqual(MeshComponentType.Edge);
        AssertBool(application.Undo()).IsTrue();
        AssertThat(application.Selection.Current.Components.Single()).IsEqual(selected);
        AssertBool(application.Undo()).IsTrue();
        AssertBool(application.Selection.Current.IsEmpty).IsTrue();
        AssertBool(application.Redo()).IsTrue();
        AssertThat(application.Selection.Current.Components.Single()).IsEqual(selected);

        application.HandleToolMouseDown(
            new EditorMouseInput(viewport.Camera, edgeScreen, IsShiftPressed: true)
        );
        AssertThat(application.Selection.Current.Components.Count).IsEqual(2);
        AssertBool(application.Selection.Current.Components.Contains(selected)).IsTrue();
        AssertBool(
                application.Selection.Current.Components.Any(component =>
                    component.Type == MeshComponentType.Edge
                )
            )
            .IsTrue();
        AssertBool(objectVisual.HasNode("SelectedVertex")).IsTrue();
        AssertBool(objectVisual.HasNode("SelectedEdge")).IsTrue();

        application.HandleToolMouseDown(
            new EditorMouseInput(viewport.Camera, vertexScreen, IsShiftPressed: true)
        );
        AssertThat(application.Selection.Current.Components.Count).IsEqual(1);
        AssertBool(application.Selection.Current.Components.Contains(selected)).IsFalse();
        AssertThat(application.UndoDisplayName).IsEqual("Deselect Component");

        application.SelectionMode.EnterObjectMode();
        AssertThat(application.Selection.Current.Components.Count).IsEqual(1);
        application.HandleToolMouseDown(
            new EditorMouseInput(viewport.Camera, faceScreen, IsShiftPressed: true)
        );
        AssertBool(application.Selection.Current.ObjectIds.Contains(meshObject.Id)).IsTrue();
        AssertThat(application.Selection.Current.Components.Count).IsEqual(1);
        AssertBool(application.Selection.Current.IsEmpty).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GlobalXRaySwitchesMutedComponentsToDepthlessMaterials()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        Node3D mapRoot = AutoFree(new Node3D());
        tree.Root.AddChild(mapRoot);
        using var application = new Application(mapRoot);
        MeshObjectId id = TestSceneObjects.AddCube(application.CurrentDocument!);
        application.SelectionMode.SelectAllComponents();
        application.Update(0.0);

        Node3D componentRoot = mapRoot.GetNode<Node3D>("ComponentSelection");
        MultiMeshInstance3D vertices = componentRoot.GetNode<MultiMeshInstance3D>(
            $"MeshObject_{id.Value}/Vertices"
        );
        MeshInstance3D edges = componentRoot.GetNode<MeshInstance3D>(
            $"MeshObject_{id.Value}/Edges"
        );

        application.SelectionMode.SetXRayEnabled(true);

        var vertexMaterial = (ShaderMaterial)((QuadMesh)vertices.Multimesh.Mesh).Material;
        var edgeMaterial = (StandardMaterial3D)edges.MaterialOverride;
        AssertBool(vertexMaterial.Shader.Code.Contains("depth_test_disabled")).IsTrue();
        AssertBool(edgeMaterial.NoDepthTest).IsTrue();

        application.SelectionMode.SetXRayEnabled(false);
        vertexMaterial = (ShaderMaterial)((QuadMesh)vertices.Multimesh.Mesh).Material;
        edgeMaterial = (StandardMaterial3D)edges.MaterialOverride;
        AssertBool(vertexMaterial.Shader.Code.Contains("depth_test_disabled")).IsFalse();
        AssertBool(edgeMaterial.NoDepthTest).IsFalse();
    }

    private static void AssertStencilXRayPass(ShaderMaterial material, int reference)
    {
        AssertBool(material.NextPass is ShaderMaterial).IsTrue();
        var xray = (ShaderMaterial)material.NextPass!;
        AssertThat(xray.RenderPriority).IsEqual(material.RenderPriority);
        AssertBool(xray.Shader.Code.Contains("depth_test_disabled", System.StringComparison.Ordinal))
            .IsTrue();
        AssertBool(
                xray.Shader.Code.Contains(
                    $"stencil_mode read, compare_equal, {reference};",
                    System.StringComparison.Ordinal
                )
            )
            .IsTrue();
    }
}
