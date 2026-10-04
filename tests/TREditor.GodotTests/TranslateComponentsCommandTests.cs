using System.Collections.Generic;
using GdUnit4;
using Godot;
using TRMesh.Mesh;
using static GdUnit4.Assertions;
using NumericsVector3 = System.Numerics.Vector3;

namespace TREditor.GodotTests;

[TestSuite]
public class TranslateComponentsCommandTests
{
    [TestCase(MeshComponentType.Vertex, 0, 1)]
    [TestCase(MeshComponentType.Edge, 0, 2)]
    [TestCase(MeshComponentType.Face, 0, 3)]
    [RequireGodotRuntime]
    public void EachComponentTypeResolvesItsAffectedVertices(
        MeshComponentType type,
        int slot,
        int expectedCount
    )
    {
        using var document = new Document();
        MeshObject meshObject = AddTriangle(document);
        var components = new HashSet<MeshComponentId> { new(meshObject.Id, type, slot) };

        AssertBool(
                ComponentVertexSelectionResolver.TryResolve(
                    document,
                    components,
                    ComponentMask.All,
                    previews: null,
                    out ComponentVertexSelection[] selections
                )
            )
            .IsTrue();

        AssertThat(selections.Length).IsEqual(1);
        AssertThat(selections[0].Vertices.Length).IsEqual(expectedCount);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void MixedComponentsResolveToOneUniqueVertexSet()
    {
        using var document = new Document();
        MeshObject meshObject = AddTriangle(document);
        var components = new HashSet<MeshComponentId>
        {
            new(meshObject.Id, MeshComponentType.Vertex, 0),
            new(meshObject.Id, MeshComponentType.Edge, 0),
            new(meshObject.Id, MeshComponentType.Face, 0),
        };

        AssertBool(
                ComponentVertexSelectionResolver.TryResolve(
                    document,
                    components,
                    ComponentMask.All,
                    previews: null,
                    out ComponentVertexSelection[] selections
                )
            )
            .IsTrue();

        AssertThat(selections.Length).IsEqual(1);
        AssertThat(selections[0].Vertices).ContainsExactly(new[] { 0, 1, 2 });
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ExecuteUndoAndRedoReplaceOnlyTheCapturedVertices()
    {
        using var document = new Document();
        using var history = new CommandHistory();
        history.SetDocument(document);
        MeshObject meshObject = AddTriangle(document);
        NumericsVector3 untouched = meshObject.Mesh.SpatialMesh.GetVertexData(1).position;
        NumericsVector3 original = meshObject.Mesh.SpatialMesh.GetVertexData(0).position;
        NumericsVector3 moved = original + new NumericsVector3(2, 3, 4);
        GDMesh movedMesh = CopyWithPositions(meshObject, [0], [moved]);

        history.Submit(new TranslateComponentsCommand([meshObject], [movedMesh]));

        AssertBool(document.TryGetObject(meshObject.Id, out MeshObject current)).IsTrue();
        AssertThat(current.Mesh.SpatialMesh.GetVertexData(0).position).IsEqual(moved);
        AssertThat(current.Mesh.SpatialMesh.GetVertexData(1).position).IsEqual(untouched);
        AssertBool(history.Undo()).IsTrue();
        AssertBool(document.TryGetObject(meshObject.Id, out current)).IsTrue();
        AssertBool(ReferenceEquals(current, meshObject)).IsTrue();
        AssertThat(current.Mesh.SpatialMesh.GetVertexData(0).position).IsEqual(original);
        AssertBool(history.Redo()).IsTrue();
        AssertBool(document.TryGetObject(meshObject.Id, out current)).IsTrue();
        AssertBool(ReferenceEquals(current.Mesh, movedMesh)).IsTrue();
        AssertThat(current.Mesh.SpatialMesh.GetVertexData(0).position).IsEqual(moved);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void MultipleObjectsCommitUnderOneMutationSerial()
    {
        using var document = new Document();
        using var history = new CommandHistory();
        history.SetDocument(document);
        MeshObject first = AddTriangle(document);
        MeshObject second = AddTriangle(document);
        NumericsVector3 firstBefore = first.Mesh.SpatialMesh.GetVertexData(0).position;
        NumericsVector3 secondBefore = second.Mesh.SpatialMesh.GetVertexData(1).position;
        GDMesh firstAfter = CopyWithPositions(
            first,
            [0],
            [firstBefore + NumericsVector3.UnitX]
        );
        GDMesh secondAfter = CopyWithPositions(
            second,
            [1],
            [secondBefore + NumericsVector3.UnitX]
        );
        var serials = new List<long>();
        document.Changed += change => serials.Add(change.MutationSerial);
        long serial = document.MutationSerial;

        history.Submit(new TranslateComponentsCommand([first, second], [firstAfter, secondAfter]));

        AssertThat(serials.Count).IsEqual(2);
        AssertThat(serials[0]).IsEqual(serial + 1);
        AssertThat(serials[1]).IsEqual(serial + 1);
        AssertThat(document.MutationSerial).IsEqual(serial + 1);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ClearingHistoryKeepsTheInstalledMeshAlive()
    {
        using var document = new Document();
        using var history = new CommandHistory();
        history.SetDocument(document);
        MeshObject before = AddTriangle(document);
        GDMesh after = CopyWithPositions(before, [0], [new NumericsVector3(3, 2, 1)]);
        history.Submit(new TranslateComponentsCommand([before], [after]));

        history.Clear();

        AssertBool(document.TryGetObject(before.Id, out MeshObject current)).IsTrue();
        AssertBool(ReferenceEquals(current.Mesh, after)).IsTrue();
        AssertThat(current.Mesh.SpatialMesh.GetVertexData(0).position)
            .IsEqual(new NumericsVector3(3, 2, 1));
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ClearingUndoneHistoryKeepsTheRestoredMeshAlive()
    {
        using var document = new Document();
        using var history = new CommandHistory();
        history.SetDocument(document);
        MeshObject before = AddTriangle(document);
        GDMesh after = CopyWithPositions(before, [0], [new NumericsVector3(3, 2, 1)]);
        history.Submit(new TranslateComponentsCommand([before], [after]));
        AssertBool(history.Undo()).IsTrue();

        history.Clear();

        AssertBool(document.TryGetObject(before.Id, out MeshObject current)).IsTrue();
        AssertBool(ReferenceEquals(current, before)).IsTrue();
        AssertThat(current.Mesh.SpatialMesh.GetVertexData(0).position)
            .IsEqual(NumericsVector3.Zero);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void AStaleObjectRejectsTheWholeTransaction()
    {
        using var document = new Document();
        using var history = new CommandHistory();
        history.SetDocument(document);
        MeshObject first = AddTriangle(document);
        MeshObject second = AddTriangle(document);
        NumericsVector3 secondBefore = second.Mesh.SpatialMesh.GetVertexData(0).position;
        GDMesh firstAfter = CopyWithPositions(first, [0], [NumericsVector3.UnitX]);
        GDMesh secondAfter = CopyWithPositions(
            second,
            [0],
            [secondBefore + NumericsVector3.UnitX]
        );
        MeshObject intervening = first with
        {
            Transform = new Transform3D(Basis.Identity, Godot.Vector3.Up),
        };
        AssertBool(document.ReplaceObject(first, intervening)).IsTrue();
        using var command = new TranslateComponentsCommand(
            [first, second],
            [firstAfter, secondAfter]
        );

        history.Submit(command);

        AssertBool(history.CanUndo).IsFalse();
        AssertThat(second.Mesh.SpatialMesh.GetVertexData(0).position).IsEqual(secondBefore);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void SettingPositionsRecomputesFaceNormalsAndMarksTheMeshDirty()
    {
        using var mesh = new GDMesh(CreateTriangle());
        mesh.RebuildSurfaceIfDirty();
        NumericsVector3 before = mesh.SpatialMesh.GetFaceData(0).normal;

        mesh.SetVertexPositions([2], [new NumericsVector3(0, 1, 1)]);

        AssertBool(mesh.Dirty).IsTrue();
        AssertBool(mesh.SpatialMesh.GetFaceData(0).normal == before).IsFalse();
    }

    private static MeshObject AddTriangle(Document document) =>
        document.Add("Triangle", Transform3D.Identity, new GDMesh(CreateTriangle()));

    private static GDMesh CopyWithPositions(
        MeshObject source,
        int[] vertices,
        NumericsVector3[] positions
    )
    {
        var mesh = new GDMesh { MeshData = source.Mesh.MeshData };
        mesh.SetVertexPositions(vertices, positions);
        return mesh;
    }

    private static SpatialMesh CreateTriangle()
    {
        var mesh = new SpatialMesh();
        int a = mesh.AddVertex(NumericsVector3.Zero);
        int b = mesh.AddVertex(NumericsVector3.UnitX);
        int c = mesh.AddVertex(NumericsVector3.UnitY);
        mesh.AddFace([a, b, c], [default, default, default]);
        return mesh;
    }
}
