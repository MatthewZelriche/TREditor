using System;
using System.Collections.Generic;
using System.Linq;
using GdUnit4;
using Godot;
using TRMesh.Mesh;
using static GdUnit4.Assertions;
using NumericsVector2 = System.Numerics.Vector2;
using NumericsVector3 = System.Numerics.Vector3;

namespace TREditor.GodotTests;

[TestSuite]
public class ExtendEdgesToolTests
{
    [TestCase]
    [RequireGodotRuntime]
    public void ExtendsACompleteBoundaryLoopWithOneQuadPerEdgeAndSupportsUndoRedo()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        SpatialMesh mesh = CreateQuad(out int face);
        MeshObject meshObject = application.CurrentDocument!.Add(
            "Quad",
            Transform3D.Identity,
            new GDMesh(mesh)
        );
        MeshComponentId[] originalSelection = Components(meshObject, FaceEdges(mesh, face));
        application.Selection.Apply(SelectionValue.ForSelections([], originalSelection));
        application.Tools.Activate(ExtendEdgesTool.ToolId);
        var tool = (ExtendEdgesTool)application.Tools.ActiveTool!;
        tool.Length = 1.0f;

        AssertThat(application.Tools.TryExecutePrimaryAction()).IsNull();

        AssertThat(mesh.VertexCount).IsEqual(8);
        AssertThat(mesh.FaceCount).IsEqual(5);
        AssertThat(mesh.EdgeCount).IsEqual(12);
        AssertThat(application.Selection.Current.Components.Count).IsEqual(4);
        AssertThat(application.UndoDisplayName).IsEqual("Extend Edges");
        foreach (MeshComponentId component in application.Selection.Current.Components)
        {
            AssertThat((int)component.Type).IsEqual((int)MeshComponentType.Edge);
            AssertBool(IsBoundary(mesh, component.Slot)).IsTrue();
        }

        float cornerOffset = 1.0f / Mathf.Sqrt(2.0f);
        NumericsVector3[] expected =
        [
            new(-cornerOffset, -cornerOffset, 0.0f),
            new(2.0f + cornerOffset, -cornerOffset, 0.0f),
            new(2.0f + cornerOffset, 2.0f + cornerOffset, 0.0f),
            new(-cornerOffset, 2.0f + cornerOffset, 0.0f),
        ];
        NumericsVector3[] added = Enumerable
            .Range(0, 8)
            .Where(slot => mesh.IsVertexAlive(slot))
            .Select(slot => mesh.GetVertexData(slot).position)
            .Where(position => !IsOriginalQuadVertex(position))
            .ToArray();
        AssertThat(added.Length).IsEqual(4);
        foreach (NumericsVector3 position in expected)
        {
            AssertBool(added.Any(candidate => NumericsVector3.Distance(candidate, position) < 0.0001f))
                .IsTrue();
        }

        AssertBool(application.Undo()).IsTrue();
        AssertThat(mesh.VertexCount).IsEqual(4);
        AssertThat(mesh.FaceCount).IsEqual(1);
        AssertBool(application.Selection.Current.Components.SetEquals(originalSelection)).IsTrue();

        AssertBool(application.Redo()).IsTrue();
        AssertThat(mesh.VertexCount).IsEqual(8);
        AssertThat(mesh.FaceCount).IsEqual(5);
        AssertThat(application.Selection.Current.Components.Count).IsEqual(4);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ExtendsAFoldedBoundaryWithSharedVerticesAndNoCornerFaces()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        SpatialMesh mesh = CreateFoldedTriangles();
        MeshObject meshObject = application.CurrentDocument!.Add(
            "Folded triangles",
            Transform3D.Identity,
            new GDMesh(mesh)
        );
        int[] boundary = BoundaryEdges(mesh);
        application.Selection.Apply(
            SelectionValue.ForSelections([], Components(meshObject, boundary))
        );
        application.Tools.Activate(ExtendEdgesTool.ToolId);
        ((ExtendEdgesTool)application.Tools.ActiveTool!).Length = 0.5f;

        AssertThat(application.Tools.TryExecutePrimaryAction()).IsNull();

        AssertThat(boundary.Length).IsEqual(4);
        AssertThat(mesh.VertexCount).IsEqual(8);
        AssertThat(mesh.FaceCount).IsEqual(6);
        AssertThat(mesh.EdgeCount).IsEqual(13);
        AssertThat(application.Selection.Current.Components.Count).IsEqual(4);
        AssertBool(Enumerable.Range(2, 4).Any(face => !IsPlanar(mesh, face))).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void MeasuresLengthInWorldUnitsForScaledObjects()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        SpatialMesh mesh = CreateQuad(out int face);
        var transform = new Transform3D(
            new Basis(
                new Vector3(2.0f, 0.0f, 0.0f),
                new Vector3(0.0f, 3.0f, 0.0f),
                Vector3.Back
            ),
            Vector3.Zero
        );
        MeshObject meshObject = application.CurrentDocument!.Add(
            "Scaled quad",
            transform,
            new GDMesh(mesh)
        );
        int edge = FaceEdges(mesh, face)[0];
        application.Selection.Apply(
            SelectionValue.ForComponent(
                new MeshComponentId(meshObject.Id, MeshComponentType.Edge, edge)
            )
        );
        application.Tools.Activate(ExtendEdgesTool.ToolId);
        ((ExtendEdgesTool)application.Tools.ActiveTool!).Length = 6.0f;

        AssertThat(application.Tools.TryExecutePrimaryAction()).IsNull();

        AssertThat(mesh.VertexCount).IsEqual(6);
        for (int vertex = 4; vertex < 6; vertex++)
        {
            Vector3 world = transform * ToGodot(mesh.GetVertexData(vertex).position);
            AssertBool(Mathf.IsEqualApprox(world.Y, -6.0f)).IsTrue();
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void RejectsEdgesThatAreNotOpenBoundaries()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        SpatialMesh mesh = SpatialMeshBuilder.CreateAabb(NumericsVector3.Zero, NumericsVector3.One);
        MeshObject meshObject = application.CurrentDocument!.Add(
            "Box",
            Transform3D.Identity,
            new GDMesh(mesh)
        );
        application.Selection.Apply(
            SelectionValue.ForComponent(
                new MeshComponentId(meshObject.Id, MeshComponentType.Edge, 0)
            )
        );
        application.Tools.Activate(ExtendEdgesTool.ToolId);

        string? failure = application.Tools.TryExecutePrimaryAction();

        AssertThat(failure).Contains("open boundary edge");
        AssertThat(mesh.VertexCount).IsEqual(8);
        AssertThat(mesh.FaceCount).IsEqual(6);
        AssertBool(application.CanUndo).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DefaultsLengthToTheCurrentGridSnapAndExposesItInTheSidebar()
    {
        PackedScene rootScene = ResourceLoader.Load<PackedScene>("res://src/ui/Root.tscn");
        Root root = AutoFree(rootScene.Instantiate<Root>());
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(root);
        Application application = root.CurrentApplication!;
        application.Snap.SetGridSnap(3.5f);
        application.SelectionMode.ToggleComponent(ComponentMask.Edge);
        application.Tools.Activate(ExtendEdgesTool.ToolId);

        var tool = (ExtendEdgesTool)application.Tools.ActiveTool!;
        SpinBox length = root.GetNode<SpinBox>(
            "UI/ToolSettingsSidebar/Panel/Margin/Contents/SettingsHost/ExtendEdgesSettings/LengthRow/LengthSpin"
        );

        AssertBool(Mathf.IsEqualApprox(tool.Length, 3.5f)).IsTrue();
        AssertBool(Mathf.IsEqualApprox((float)length.Value, 3.5f)).IsTrue();
        length.Value = 7.0;
        AssertBool(Mathf.IsEqualApprox(tool.Length, 7.0f)).IsTrue();
    }

    private static SpatialMesh CreateQuad(out int face)
    {
        var mesh = new SpatialMesh();
        int a = mesh.AddVertex(NumericsVector3.Zero);
        int b = mesh.AddVertex(new NumericsVector3(2.0f, 0.0f, 0.0f));
        int c = mesh.AddVertex(new NumericsVector3(2.0f, 2.0f, 0.0f));
        int d = mesh.AddVertex(new NumericsVector3(0.0f, 2.0f, 0.0f));
        face = mesh.AddFace(
            [a, b, c, d],
            [NumericsVector2.Zero, NumericsVector2.UnitX, NumericsVector2.One, NumericsVector2.UnitY]
        );
        return mesh;
    }

    private static SpatialMesh CreateFoldedTriangles()
    {
        var mesh = new SpatialMesh();
        int a = mesh.AddVertex(NumericsVector3.Zero);
        int b = mesh.AddVertex(new NumericsVector3(1.0f, 0.0f, 0.0f));
        int c = mesh.AddVertex(new NumericsVector3(0.0f, 2.0f, 0.0f));
        int d = mesh.AddVertex(new NumericsVector3(0.0f, 0.0f, 1.0f));
        mesh.AddFace(
            [a, b, c],
            [NumericsVector2.Zero, NumericsVector2.UnitX, NumericsVector2.UnitY]
        );
        mesh.AddFace(
            [b, a, d],
            [NumericsVector2.Zero, NumericsVector2.UnitX, NumericsVector2.UnitY]
        );
        return mesh;
    }

    private static int[] FaceEdges(SpatialMesh mesh, int face)
    {
        var edges = new List<int>();
        int start = mesh.GetFaceData(face).topology.AdjacentHalfEdge;
        int current = start;
        do
        {
            int twin = mesh.GetHalfEdgeData(current).topology.TwinHalfEdge;
            edges.Add(Math.Min(current, twin));
            current = mesh.GetHalfEdgeData(current).topology.NextHalfEdge;
        } while (current != start);
        return [.. edges];
    }

    private static int[] BoundaryEdges(SpatialMesh mesh)
    {
        var edges = new List<int>();
        mesh.WithHalfEdges((slots, topology, _) =>
        {
            for (int index = 0; index < slots.Length; index++)
            {
                int slot = slots[index];
                int twin = topology[index].TwinHalfEdge;
                if (slot < twin && IsBoundary(mesh, slot))
                    edges.Add(slot);
            }
        });
        return [.. edges];
    }

    private static MeshComponentId[] Components(MeshObject meshObject, IEnumerable<int> edges) =>
        edges
            .Select(edge => new MeshComponentId(meshObject.Id, MeshComponentType.Edge, edge))
            .ToArray();

    private static bool IsBoundary(SpatialMesh mesh, int edge)
    {
        HalfEdge first = mesh.GetHalfEdgeData(edge).topology;
        HalfEdge second = mesh.GetHalfEdgeData(first.TwinHalfEdge).topology;
        return mesh.IsFaceAlive(first.AdjacentFace) != mesh.IsFaceAlive(second.AdjacentFace);
    }

    private static bool IsOriginalQuadVertex(NumericsVector3 position) =>
        position == NumericsVector3.Zero
        || position == new NumericsVector3(2.0f, 0.0f, 0.0f)
        || position == new NumericsVector3(2.0f, 2.0f, 0.0f)
        || position == new NumericsVector3(0.0f, 2.0f, 0.0f);

    private static bool IsPlanar(SpatialMesh mesh, int face)
    {
        int start = mesh.GetFaceData(face).topology.AdjacentHalfEdge;
        var positions = new List<NumericsVector3>();
        int current = start;
        do
        {
            int vertex = mesh.GetHalfEdgeData(current).topology.SourceVertex;
            positions.Add(mesh.GetVertexData(vertex).position);
            current = mesh.GetHalfEdgeData(current).topology.NextHalfEdge;
        } while (current != start);

        if (positions.Count < 4)
            return true;
        NumericsVector3 normal = NumericsVector3.Cross(
            positions[1] - positions[0],
            positions[2] - positions[0]
        );
        return MathF.Abs(NumericsVector3.Dot(normal, positions[3] - positions[0])) < 0.0001f;
    }

    private static Vector3 ToGodot(NumericsVector3 value) => new(value.X, value.Y, value.Z);
}
