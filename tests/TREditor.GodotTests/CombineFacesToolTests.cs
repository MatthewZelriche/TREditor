using System.Linq;
using GdUnit4;
using Godot;
using TRMesh.Mesh;
using static GdUnit4.Assertions;
using NumericsVector2 = System.Numerics.Vector2;
using NumericsVector3 = System.Numerics.Vector3;

namespace TREditor.GodotTests;

[TestSuite]
public class CombineFacesToolTests
{
    [TestCase]
    [RequireGodotRuntime]
    public void CombinesAConnectedFaceIslandAndSupportsUndoRedo()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        SpatialMesh mesh = CreateQuadFan(out int center, out int[] faces);
        MeshObject meshObject = application.CurrentDocument!.Add(
            "Quad fan",
            Transform3D.Identity,
            new GDMesh(mesh)
        );
        MeshComponentId[] selected =
        [
            new(meshObject.Id, MeshComponentType.Face, faces[0]),
            new(meshObject.Id, MeshComponentType.Face, faces[1]),
            new(meshObject.Id, MeshComponentType.Face, faces[2]),
            new(meshObject.Id, MeshComponentType.Face, faces[3]),
        ];
        application.Selection.Apply(SelectionValue.ForSelections([], selected));
        application.Tools.Activate(CombineFacesTool.ToolId);

        AssertThat(application.Tools.TryExecutePrimaryAction()).IsNull();

        AssertThat(mesh.FaceCount).IsEqual(1);
        AssertThat(mesh.EdgeCount).IsEqual(4);
        AssertThat(mesh.VertexCount).IsEqual(4);
        AssertBool(mesh.IsVertexAlive(center)).IsFalse();
        MeshComponentId survivor = application.Selection.Current.Components.Single();
        AssertThat((int)survivor.Type).IsEqual((int)MeshComponentType.Face);
        AssertThat(survivor.Slot).IsEqual(faces.Min());
        AssertThat(application.UndoDisplayName).IsEqual("Combine Faces");

        AssertBool(application.Undo()).IsTrue();
        AssertThat(mesh.FaceCount).IsEqual(4);
        foreach (MeshComponentId face in selected)
            AssertBool(application.Selection.Current.Components.Contains(face)).IsTrue();

        AssertBool(application.Redo()).IsTrue();
        AssertThat(mesh.FaceCount).IsEqual(1);
        AssertThat(application.Selection.Current.Components.Single()).IsEqual(survivor);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void RejectsSelectionsContainingDisconnectedFaceIslands()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        SpatialMesh mesh = CreatePartlyDisconnectedFaces(
            out int first,
            out int second,
            out int disconnected
        );
        MeshObject meshObject = application.CurrentDocument!.Add(
            "Disconnected faces",
            Transform3D.Identity,
            new GDMesh(mesh)
        );
        application.Selection.Apply(
            SelectionValue.ForSelections(
                [],
                [
                    new(meshObject.Id, MeshComponentType.Face, first),
                    new(meshObject.Id, MeshComponentType.Face, second),
                    new(meshObject.Id, MeshComponentType.Face, disconnected),
                ]
            )
        );
        application.Tools.Activate(CombineFacesTool.ToolId);

        EditorToolAction action = ((CombineFacesTool)application.Tools.ActiveTool!)
            .CreatePrimaryAction(application.ToolContext);

        AssertThat(action.UnavailableReason).Contains("one connected face island");
        AssertThat(mesh.FaceCount).IsEqual(3);
        AssertBool(application.CanUndo).IsFalse();
    }

    private static SpatialMesh CreateQuadFan(out int center, out int[] faces)
    {
        var mesh = new SpatialMesh();
        int a = mesh.AddVertex(NumericsVector3.Zero);
        int b = mesh.AddVertex(NumericsVector3.UnitX);
        int c = mesh.AddVertex(NumericsVector3.One);
        int d = mesh.AddVertex(NumericsVector3.UnitY);
        center = mesh.AddVertex(new NumericsVector3(0.5f, 0.5f, 0.0f));
        faces =
        [
            mesh.AddFace([a, b, center], TriangleUvs),
            mesh.AddFace([b, c, center], TriangleUvs),
            mesh.AddFace([c, d, center], TriangleUvs),
            mesh.AddFace([d, a, center], TriangleUvs),
        ];
        return mesh;
    }

    private static SpatialMesh CreatePartlyDisconnectedFaces(
        out int first,
        out int second,
        out int disconnected
    )
    {
        var mesh = new SpatialMesh();
        int a = mesh.AddVertex(NumericsVector3.Zero);
        int b = mesh.AddVertex(NumericsVector3.UnitX);
        int c = mesh.AddVertex(NumericsVector3.One);
        int d = mesh.AddVertex(NumericsVector3.UnitY);
        int e = mesh.AddVertex(new NumericsVector3(2.0f, 0.0f, 0.0f));
        int f = mesh.AddVertex(new NumericsVector3(2.0f, 1.0f, 0.0f));
        first = mesh.AddFace([a, b, c, d], QuadUvs);
        second = mesh.AddFace([b, e, f, c], QuadUvs);

        int g = mesh.AddVertex(new NumericsVector3(4.0f, 0.0f, 0.0f));
        int h = mesh.AddVertex(new NumericsVector3(5.0f, 0.0f, 0.0f));
        int i = mesh.AddVertex(new NumericsVector3(5.0f, 1.0f, 0.0f));
        int j = mesh.AddVertex(new NumericsVector3(4.0f, 1.0f, 0.0f));
        disconnected = mesh.AddFace([g, h, i, j], QuadUvs);
        return mesh;
    }

    private static readonly NumericsVector2[] TriangleUvs =
    [
        NumericsVector2.Zero,
        NumericsVector2.UnitX,
        NumericsVector2.UnitY,
    ];

    private static readonly NumericsVector2[] QuadUvs =
    [
        NumericsVector2.Zero,
        NumericsVector2.UnitX,
        NumericsVector2.One,
        NumericsVector2.UnitY,
    ];
}
