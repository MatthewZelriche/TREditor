using System.Linq;
using GdUnit4;
using Godot;
using TRMesh.Mesh;
using static GdUnit4.Assertions;
using NumericsVector2 = System.Numerics.Vector2;
using NumericsVector3 = System.Numerics.Vector3;

namespace TREditor.GodotTests;

[TestSuite]
public class MergeVerticesToolTests
{
    [TestCase]
    [RequireGodotRuntime]
    public void PositionModesPreserveSelectionOrderAndUndoRedo()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        SpatialMesh mesh = CreateQuad(out int first, out int last);
        MeshObject meshObject = application.CurrentDocument!.Add(
            "Quad",
            Transform3D.Identity,
            new GDMesh(mesh)
        );
        var firstComponent = new MeshComponentId(
            meshObject.Id,
            MeshComponentType.Vertex,
            first
        );
        var lastComponent = new MeshComponentId(
            meshObject.Id,
            MeshComponentType.Vertex,
            last
        );
        SelectionValue selected = SelectionValue.ForSelections(
            [],
            [firstComponent, lastComponent]
        );
        application.Selection.Apply(selected);
        var tool = new MergeVerticesTool();

        tool.Merge(application.ToolContext);

        AssertBool(mesh.IsVertexAlive(first)).IsFalse();
        AssertBool(mesh.IsVertexAlive(last)).IsTrue();
        AssertThat(mesh.GetVertexData(last).position).IsEqual(new NumericsVector3(2f, 0f, 0f));
        AssertThat(application.Selection.Current.Components.Single()).IsEqual(lastComponent);
        AssertThat(application.UndoDisplayName).IsEqual("Merge Vertices");

        AssertBool(application.Undo()).IsTrue();
        AssertBool(mesh.IsVertexAlive(first)).IsTrue();
        AssertThat(application.Selection.Current.OrderedComponents[0]).IsEqual(firstComponent);
        AssertThat(application.Selection.Current.OrderedComponents[1]).IsEqual(lastComponent);

        AssertBool(application.Redo()).IsTrue();
        AssertBool(mesh.IsVertexAlive(first)).IsFalse();
        AssertBool(application.Undo()).IsTrue();

        tool.Position = VertexMergePosition.Centroid;
        tool.Merge(application.ToolContext);
        AssertThat(mesh.GetVertexData(last).position).IsEqual(new NumericsVector3(1f, 0f, 0f));
        AssertBool(application.Undo()).IsTrue();

        tool.Position = VertexMergePosition.First;
        tool.Merge(application.ToolContext);
        AssertBool(mesh.IsVertexAlive(first)).IsTrue();
        AssertBool(mesh.IsVertexAlive(last)).IsFalse();
        AssertThat(mesh.GetVertexData(first).position).IsEqual(NumericsVector3.Zero);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void InvalidIslandsReportARecoverableFailureWithoutChangingTheMesh()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        var mesh = new SpatialMesh();
        int first = AddTriangle(mesh, NumericsVector3.Zero);
        int second = AddTriangle(mesh, new NumericsVector3(3f, 0f, 0f));
        MeshObject meshObject = application.CurrentDocument!.Add(
            "Islands",
            Transform3D.Identity,
            new GDMesh(mesh)
        );
        application.Selection.Apply(
            SelectionValue.ForSelections(
                [],
                [
                    new(meshObject.Id, MeshComponentType.Vertex, first),
                    new(meshObject.Id, MeshComponentType.Vertex, second),
                ]
            )
        );
        string? failure = null;
        application.OperationFailed += (_, exception) => failure = exception.Message;

        new MergeVerticesTool().Merge(application.ToolContext);

        AssertThat(failure).Contains("cannot be connected");
        AssertThat(mesh.VertexCount).IsEqual(6);
        AssertThat(mesh.FaceCount).IsEqual(2);
        AssertBool(application.CanUndo).IsFalse();
    }

    private static SpatialMesh CreateQuad(out int first, out int last)
    {
        var mesh = new SpatialMesh();
        first = mesh.AddVertex(NumericsVector3.Zero);
        last = mesh.AddVertex(new NumericsVector3(2f, 0f, 0f));
        int c = mesh.AddVertex(new NumericsVector3(2f, 2f, 0f));
        int d = mesh.AddVertex(new NumericsVector3(0f, 2f, 0f));
        mesh.AddFace(
            [first, last, c, d],
            [NumericsVector2.Zero, NumericsVector2.UnitX, NumericsVector2.One, NumericsVector2.UnitY]
        );
        return mesh;
    }

    private static int AddTriangle(SpatialMesh mesh, NumericsVector3 origin)
    {
        int first = mesh.AddVertex(origin);
        int second = mesh.AddVertex(origin + NumericsVector3.UnitX);
        int third = mesh.AddVertex(origin + NumericsVector3.UnitY);
        mesh.AddFace(
            [first, second, third],
            [NumericsVector2.Zero, NumericsVector2.UnitX, NumericsVector2.UnitY]
        );
        return first;
    }
}
