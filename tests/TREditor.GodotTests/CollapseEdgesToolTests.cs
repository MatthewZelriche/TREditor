using System.Linq;
using GdUnit4;
using Godot;
using TRMesh.Mesh;
using static GdUnit4.Assertions;
using NumericsVector2 = System.Numerics.Vector2;
using NumericsVector3 = System.Numerics.Vector3;

namespace TREditor.GodotTests;

[TestSuite]
public class CollapseEdgesToolTests
{
    [TestCase]
    [RequireGodotRuntime]
    public void CollapseMovesEndpointsToMidpointAndSupportsUndoRedo()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        SpatialMesh mesh = CreateQuad(out int edge, out int source, out int target);
        MeshObject meshObject = application.CurrentDocument!.Add(
            "Quad",
            Transform3D.Identity,
            new GDMesh(mesh)
        );
        var edgeComponent = new MeshComponentId(meshObject.Id, MeshComponentType.Edge, edge);
        application.Selection.Apply(SelectionValue.ForComponent(edgeComponent));
        application.Tools.Activate(CollapseEdgesTool.ToolId);

        AssertThat(application.Tools.TryExecutePrimaryAction()).IsNull();

        AssertBool(mesh.IsVertexAlive(source)).IsFalse();
        AssertBool(mesh.IsVertexAlive(target)).IsTrue();
        AssertThat(mesh.GetVertexData(target).position).IsEqual(new NumericsVector3(1f, 0f, 0f));
        AssertThat(application.Selection.Current.Components.Single())
            .IsEqual(new MeshComponentId(meshObject.Id, MeshComponentType.Vertex, target));
        AssertThat(application.UndoDisplayName).IsEqual("Collapse Edges");

        AssertBool(application.Undo()).IsTrue();
        AssertBool(mesh.IsVertexAlive(source)).IsTrue();
        AssertThat(application.Selection.Current.Components.Single()).IsEqual(edgeComponent);

        AssertBool(application.Redo()).IsTrue();
        AssertBool(mesh.IsVertexAlive(source)).IsFalse();
        AssertThat(mesh.GetVertexData(target).position).IsEqual(new NumericsVector3(1f, 0f, 0f));
    }

    [TestCase]
    [RequireGodotRuntime]
    public void SidebarActionTracksSelectionReadinessAndExplainsWhyItIsDisabled()
    {
        PackedScene rootScene = ResourceLoader.Load<PackedScene>("res://src/ui/Root.tscn");
        Root root = AutoFree(rootScene.Instantiate<Root>());
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(root);
        Application application = root.CurrentApplication!;
        application.SelectionMode.ToggleComponent(ComponentMask.Edge);
        application.Tools.Activate(CollapseEdgesTool.ToolId);

        ToolSettingsSidebar sidebar = root.GetNode<ToolSettingsSidebar>("UI/ToolSettingsSidebar");
        Button actionButton = sidebar.GetNode<Button>(
            "Panel/Margin/Contents/PrimaryActionRow/PrimaryActionButton"
        );
        Label warning = sidebar.GetNode<Label>(
            "Panel/Margin/Contents/PrimaryActionRow/PrimaryActionWarning"
        );

        AssertBool(actionButton.Disabled).IsTrue();
        AssertThat(actionButton.TooltipText).IsEqual("Select at least one edge from one mesh.");
        AssertBool(warning.Visible).IsTrue();

        SpatialMesh mesh = CreateQuad(out int edge, out _, out _);
        MeshObject meshObject = application.CurrentDocument!.Add(
            "Quad",
            Transform3D.Identity,
            new GDMesh(mesh)
        );
        application.Selection.Apply(
            SelectionValue.ForSelections(
                [],
                [
                    new(meshObject.Id, MeshComponentType.Edge, edge),
                    new(meshObject.Id, MeshComponentType.Vertex, 0),
                ]
            )
        );

        AssertBool(actionButton.Disabled).IsTrue();
        AssertThat(warning.TooltipText).Contains("Only edges can be collapsed");

        application.Selection.Apply(
            SelectionValue.ForComponent(new MeshComponentId(meshObject.Id, MeshComponentType.Edge, edge))
        );

        AssertBool(actionButton.Disabled).IsFalse();
        AssertThat(actionButton.TooltipText).IsEqual(string.Empty);
        actionButton.EmitSignal(BaseButton.SignalName.Pressed);
        AssertThat(mesh.VertexCount).IsEqual(3);
        AssertThat(application.UndoDisplayName).IsEqual("Collapse Edges");
    }

    private static SpatialMesh CreateQuad(out int edge, out int source, out int target)
    {
        var mesh = new SpatialMesh();
        source = mesh.AddVertex(NumericsVector3.Zero);
        target = mesh.AddVertex(new NumericsVector3(2f, 0f, 0f));
        int c = mesh.AddVertex(new NumericsVector3(2f, 2f, 0f));
        int d = mesh.AddVertex(new NumericsVector3(0f, 2f, 0f));
        int face = mesh.AddFace(
            [source, target, c, d],
            [NumericsVector2.Zero, NumericsVector2.UnitX, NumericsVector2.One, NumericsVector2.UnitY]
        );
        edge = mesh.GetFaceData(face).topology.AdjacentHalfEdge;
        return mesh;
    }
}
