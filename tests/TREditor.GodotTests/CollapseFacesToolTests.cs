using System.Linq;
using GdUnit4;
using Godot;
using TRMesh.Mesh;
using static GdUnit4.Assertions;
using NumericsVector2 = System.Numerics.Vector2;
using NumericsVector3 = System.Numerics.Vector3;

namespace TREditor.GodotTests;

[TestSuite]
public class CollapseFacesToolTests
{
    [TestCase]
    [RequireGodotRuntime]
    public void CollapseMovesFaceVerticesToCentroidAndSupportsUndoRedo()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        SpatialMesh mesh = CreateQuad(out int face);
        MeshObject meshObject = application.CurrentDocument!.Add(
            "Quad",
            Transform3D.Identity,
            new GDMesh(mesh)
        );
        var faceComponent = new MeshComponentId(meshObject.Id, MeshComponentType.Face, face);
        application.Selection.Apply(SelectionValue.ForComponent(faceComponent));
        application.Tools.Activate(CollapseFacesTool.ToolId);

        AssertThat(application.Tools.TryExecutePrimaryAction()).IsNull();

        AssertThat(mesh.VertexCount).IsEqual(1);
        AssertThat(mesh.FaceCount).IsEqual(0);
        MeshComponentId survivor = application.Selection.Current.Components.Single();
        AssertThat((int)survivor.Type).IsEqual((int)MeshComponentType.Vertex);
        AssertThat(mesh.GetVertexData(survivor.Slot).position)
            .IsEqual(new NumericsVector3(1f, 1f, 0f));
        AssertThat(application.UndoDisplayName).IsEqual("Collapse Faces");

        AssertBool(application.Undo()).IsTrue();
        AssertThat(mesh.VertexCount).IsEqual(4);
        AssertThat(mesh.FaceCount).IsEqual(1);
        AssertThat(application.Selection.Current.Components.Single()).IsEqual(faceComponent);

        AssertBool(application.Redo()).IsTrue();
        AssertThat(mesh.VertexCount).IsEqual(1);
        AssertThat(mesh.GetVertexData(survivor.Slot).position)
            .IsEqual(new NumericsVector3(1f, 1f, 0f));
    }

    [TestCase]
    [RequireGodotRuntime]
    public void MultipleFacesUseEachSharedVertexOnceInTheCentroid()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        SpatialMesh mesh = CreateAdjacentQuads(out int left, out int right);
        MeshObject meshObject = application.CurrentDocument!.Add(
            "Quads",
            Transform3D.Identity,
            new GDMesh(mesh)
        );
        application.Selection.Apply(
            SelectionValue.ForSelections(
                [],
                [
                    new(meshObject.Id, MeshComponentType.Face, left),
                    new(meshObject.Id, MeshComponentType.Face, right),
                ]
            )
        );
        application.Tools.Activate(CollapseFacesTool.ToolId);

        AssertThat(application.Tools.TryExecutePrimaryAction()).IsNull();

        AssertThat(mesh.VertexCount).IsEqual(1);
        MeshComponentId survivor = application.Selection.Current.Components.Single();
        AssertThat(mesh.GetVertexData(survivor.Slot).position)
            .IsEqual(new NumericsVector3(-1f / 3f, 1f, 0f));
    }

    [TestCase]
    [RequireGodotRuntime]
    public void SidebarActionTracksSelectionReadinessAndExplainsWhyItIsDisabled()
    {
        PackedScene rootScene = ResourceLoader.Load<PackedScene>("res://src/ui/Root.tscn");
        Root root = AutoFree(rootScene.Instantiate<Root>());
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(root);
        Application application = root.CurrentApplication!;
        application.SelectionMode.ToggleComponent(ComponentMask.Face);
        application.Tools.Activate(CollapseFacesTool.ToolId);

        ToolSettingsSidebar sidebar = root.GetNode<ToolSettingsSidebar>("UI/ToolSettingsSidebar");
        Button actionButton = sidebar.GetNode<Button>(
            "Panel/Margin/Contents/PrimaryActionRow/PrimaryActionButton"
        );
        Label warning = sidebar.GetNode<Label>(
            "Panel/Margin/Contents/PrimaryActionRow/PrimaryActionWarning"
        );

        AssertBool(actionButton.Disabled).IsTrue();
        AssertThat(actionButton.TooltipText).IsEqual("Select at least one face from one mesh.");
        AssertBool(warning.Visible).IsTrue();

        SpatialMesh mesh = CreateQuad(out int face);
        MeshObject meshObject = application.CurrentDocument!.Add(
            "Quad",
            Transform3D.Identity,
            new GDMesh(mesh)
        );
        application.Selection.Apply(
            SelectionValue.ForSelections(
                [],
                [
                    new(meshObject.Id, MeshComponentType.Face, face),
                    new(meshObject.Id, MeshComponentType.Edge, 0),
                ]
            )
        );

        AssertBool(actionButton.Disabled).IsTrue();
        AssertThat(warning.TooltipText).Contains("Only faces can be collapsed");

        application.Selection.Apply(
            SelectionValue.ForComponent(new MeshComponentId(meshObject.Id, MeshComponentType.Face, face))
        );

        AssertBool(actionButton.Disabled).IsFalse();
        AssertThat(actionButton.TooltipText).IsEqual(string.Empty);
        actionButton.EmitSignal(BaseButton.SignalName.Pressed);
        AssertThat(mesh.VertexCount).IsEqual(1);
        AssertThat(application.UndoDisplayName).IsEqual("Collapse Faces");
    }

    private static SpatialMesh CreateQuad(out int face)
    {
        var mesh = new SpatialMesh();
        int a = mesh.AddVertex(NumericsVector3.Zero);
        int b = mesh.AddVertex(new NumericsVector3(2f, 0f, 0f));
        int c = mesh.AddVertex(new NumericsVector3(2f, 2f, 0f));
        int d = mesh.AddVertex(new NumericsVector3(0f, 2f, 0f));
        face = mesh.AddFace(
            [a, b, c, d],
            [NumericsVector2.Zero, NumericsVector2.UnitX, NumericsVector2.One, NumericsVector2.UnitY]
        );
        return mesh;
    }

    private static SpatialMesh CreateAdjacentQuads(out int left, out int right)
    {
        var mesh = new SpatialMesh();
        int a = mesh.AddVertex(new NumericsVector3(-2f, 0f, 0f));
        int b = mesh.AddVertex(NumericsVector3.Zero);
        int c = mesh.AddVertex(new NumericsVector3(0f, 2f, 0f));
        int d = mesh.AddVertex(new NumericsVector3(-2f, 2f, 0f));
        int e = mesh.AddVertex(new NumericsVector3(1f, 0f, 0f));
        int f = mesh.AddVertex(new NumericsVector3(1f, 2f, 0f));
        left = mesh.AddFace(
            [a, b, c, d],
            [NumericsVector2.Zero, NumericsVector2.UnitX, NumericsVector2.One, NumericsVector2.UnitY]
        );
        right = mesh.AddFace(
            [b, e, f, c],
            [NumericsVector2.Zero, NumericsVector2.UnitX, NumericsVector2.One, NumericsVector2.UnitY]
        );
        return mesh;
    }
}
