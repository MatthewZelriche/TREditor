using System.Collections.Generic;
using System.Linq;
using GdUnit4;
using Godot;
using TRMesh.Mesh;
using static GdUnit4.Assertions;
using NumericsVector3 = System.Numerics.Vector3;

namespace TREditor.GodotTests;

[TestSuite]
public class ExtrudeFacesToolTests
{
    [TestCase]
    [RequireGodotRuntime]
    public void ExtrudesSelectedFacesAlongTheirNormalAndSupportsUndoRedo()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        SpatialMesh mesh = SpatialMeshBuilder.CreateAabb(NumericsVector3.Zero, NumericsVector3.One);
        MeshObject meshObject = application.CurrentDocument!.Add(
            "Box",
            Transform3D.Identity,
            new GDMesh(mesh)
        );
        const int face = 5;
        NumericsVector3 normal = mesh.GetFaceData(face).normal;
        NumericsVector3[] original = FacePositions(mesh, face);
        var originalSelection = new MeshComponentId(
            meshObject.Id,
            MeshComponentType.Face,
            face
        );
        application.Selection.Apply(SelectionValue.ForComponent(originalSelection));
        application.Tools.Activate(ExtrudeFacesTool.ToolId);
        ((ExtrudeFacesTool)application.Tools.ActiveTool!).Length = 2.0f;

        AssertThat(application.Tools.TryExecutePrimaryAction()).IsNull();

        AssertThat(mesh.VertexCount).IsEqual(12);
        AssertThat(mesh.FaceCount).IsEqual(10);
        AssertThat(application.Selection.Current.Components.Count).IsEqual(1);
        MeshComponentId cap = application.Selection.Current.Components.Single();
        AssertThat((int)cap.Type).IsEqual((int)MeshComponentType.Face);
        NumericsVector3[] capPositions = FacePositions(mesh, cap.Slot);
        foreach (NumericsVector3 position in original)
        {
            NumericsVector3 expected = position + normal * 2.0f;
            AssertBool(capPositions.Any(candidate => candidate == expected)).IsTrue();
        }
        AssertThat(application.UndoDisplayName).IsEqual("Extrude Faces");

        AssertBool(application.Undo()).IsTrue();
        AssertThat(mesh.VertexCount).IsEqual(8);
        AssertThat(mesh.FaceCount).IsEqual(6);
        AssertBool(application.Selection.Current.Components.Contains(originalSelection)).IsTrue();

        AssertBool(application.Redo()).IsTrue();
        AssertThat(mesh.VertexCount).IsEqual(12);
        AssertThat(mesh.FaceCount).IsEqual(10);
        AssertThat(application.Selection.Current.Components.Count).IsEqual(1);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DefaultsLengthToGridSnapAndRejectsNonFaceSelections()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        SpatialMesh mesh = SpatialMeshBuilder.CreateAabb(NumericsVector3.Zero, NumericsVector3.One);
        MeshObject meshObject = application.CurrentDocument!.Add(
            "Box",
            Transform3D.Identity,
            new GDMesh(mesh)
        );
        application.Snap.SetGridSnap(3.5f);
        application.Selection.Apply(
            SelectionValue.ForComponent(
                new MeshComponentId(meshObject.Id, MeshComponentType.Edge, 0)
            )
        );
        application.Tools.Activate(ExtrudeFacesTool.ToolId);
        var tool = (ExtrudeFacesTool)application.Tools.ActiveTool!;
        EditorToolAction action = tool.CreatePrimaryAction(application.ToolContext);

        AssertBool(Mathf.IsEqualApprox(tool.Length, 3.5f)).IsTrue();
        AssertThat(action.UnavailableReason)
            .Contains("Only faces can be extruded");
        AssertThat(mesh.FaceCount).IsEqual(6);
    }

    private static NumericsVector3[] FacePositions(SpatialMesh mesh, int face)
    {
        var positions = new List<NumericsVector3>();
        int start = mesh.GetFaceData(face).topology.AdjacentHalfEdge;
        int current = start;
        do
        {
            HalfEdge edge = mesh.GetHalfEdgeData(current).topology;
            positions.Add(mesh.GetVertexData(edge.SourceVertex).position);
            current = edge.NextHalfEdge;
        } while (current != start);
        return [.. positions];
    }
}
