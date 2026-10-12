using System.Linq;
using GdUnit4;
using Godot;
using TRMesh.Mesh;
using static GdUnit4.Assertions;
using NumericsVector2 = System.Numerics.Vector2;
using NumericsVector3 = System.Numerics.Vector3;

namespace TREditor.GodotTests;

[TestSuite]
public class DissolveEdgesToolTests
{
    [TestCase]
    [RequireGodotRuntime]
    public void DissolvesSelectedEdgeAndSupportsUndoRedo()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        SpatialMesh mesh = CreateSplitQuad(out int diagonal, out _);
        MeshObject meshObject = application.CurrentDocument!.Add(
            "Split quad",
            Transform3D.Identity,
            new GDMesh(mesh)
        );
        var selected = new MeshComponentId(meshObject.Id, MeshComponentType.Edge, diagonal);
        application.Selection.Apply(SelectionValue.ForComponent(selected));
        application.Tools.Activate(DissolveEdgesTool.ToolId);

        AssertThat(application.Tools.TryExecutePrimaryAction()).IsNull();

        AssertThat(mesh.FaceCount).IsEqual(1);
        AssertThat(mesh.EdgeCount).IsEqual(4);
        AssertBool(application.Selection.Current.IsEmpty).IsTrue();
        AssertThat(application.UndoDisplayName).IsEqual("Dissolve Edges");

        AssertBool(application.Undo()).IsTrue();
        AssertThat(mesh.FaceCount).IsEqual(2);
        AssertBool(application.Selection.Current.Components.Contains(selected)).IsTrue();

        AssertBool(application.Redo()).IsTrue();
        AssertThat(mesh.FaceCount).IsEqual(1);
        AssertBool(application.Selection.Current.IsEmpty).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void RejectsBoundaryEdgesBeforeSubmittingACommand()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        SpatialMesh mesh = CreateSplitQuad(out _, out int boundary);
        MeshObject meshObject = application.CurrentDocument!.Add(
            "Split quad",
            Transform3D.Identity,
            new GDMesh(mesh)
        );
        application.Selection.Apply(
            SelectionValue.ForComponent(
                new MeshComponentId(meshObject.Id, MeshComponentType.Edge, boundary)
            )
        );
        application.Tools.Activate(DissolveEdgesTool.ToolId);

        EditorToolAction action = ((DissolveEdgesTool)application.Tools.ActiveTool!)
            .CreatePrimaryAction(application.ToolContext);

        AssertThat(action.UnavailableReason).Contains("between two distinct faces");
        AssertThat(mesh.FaceCount).IsEqual(2);
        AssertBool(application.CanUndo).IsFalse();
    }

    private static SpatialMesh CreateSplitQuad(out int diagonal, out int boundary)
    {
        var mesh = new SpatialMesh();
        int a = mesh.AddVertex(NumericsVector3.Zero);
        int b = mesh.AddVertex(NumericsVector3.UnitX);
        int c = mesh.AddVertex(NumericsVector3.One);
        int d = mesh.AddVertex(NumericsVector3.UnitY);
        int face = mesh.AddFace(
            [a, b, c, d],
            [NumericsVector2.Zero, NumericsVector2.UnitX, NumericsVector2.One, NumericsVector2.UnitY]
        );
        boundary = mesh.GetFaceData(face).topology.AdjacentHalfEdge;
        diagonal = mesh.SplitFace(face, a, c);
        return mesh;
    }
}
