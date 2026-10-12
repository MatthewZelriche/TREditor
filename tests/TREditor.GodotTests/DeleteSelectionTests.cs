using System;
using GdUnit4;
using Godot;
using TRMesh.Mesh;
using static GdUnit4.Assertions;
using NumericsVector2 = System.Numerics.Vector2;
using NumericsVector3 = System.Numerics.Vector3;

namespace TREditor.GodotTests;

[TestSuite]
public class DeleteSelectionTests
{
    [TestCase]
    [RequireGodotRuntime]
    public void DeleteRemovesSelectedObjectsThroughHistory()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        MeshObject meshObject = application.CurrentDocument!.Add(
            "Cube",
            Transform3D.Identity,
            new GDMesh(SpatialMeshBuilder.CreateAabb(NumericsVector3.Zero, NumericsVector3.One))
        );
        application.Selection.Apply(SelectionValue.ForObject(meshObject.Id));

        AssertThat(application.TryDeleteSelection()).IsNull();

        AssertThat(application.CurrentDocument.ObjectCount).IsEqual(0);
        AssertBool(application.Selection.Current.IsEmpty).IsTrue();
        AssertThat(application.UndoDisplayName).IsEqual("Delete Object");

        AssertBool(application.Undo()).IsTrue();
        AssertThat(application.CurrentDocument.ObjectCount).IsEqual(1);
        AssertThat(application.Selection.Current.ObjectIds.Count).IsEqual(1);

        AssertBool(application.Redo()).IsTrue();
        AssertThat(application.CurrentDocument.ObjectCount).IsEqual(0);
    }

    [TestCase(MeshComponentType.Vertex)]
    [TestCase(MeshComponentType.Edge)]
    [TestCase(MeshComponentType.Face)]
    [RequireGodotRuntime]
    public void DeleteRemovesSelectedComponentsThroughHistory(
        MeshComponentType componentType
    )
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        SpatialMesh mesh = CreateQuad(out int face);
        int halfEdge = mesh.GetFaceData(face).topology.AdjacentHalfEdge;
        HalfEdge edge = mesh.GetHalfEdgeData(halfEdge).topology;
        int slot = componentType switch
        {
            MeshComponentType.Vertex => edge.SourceVertex,
            MeshComponentType.Edge => Math.Min(halfEdge, edge.TwinHalfEdge),
            MeshComponentType.Face => face,
            _ => throw new ArgumentOutOfRangeException(nameof(componentType)),
        };
        MeshObject meshObject = application.CurrentDocument!.Add(
            "Quad",
            Transform3D.Identity,
            new GDMesh(mesh)
        );
        application.Selection.Apply(
            SelectionValue.ForComponent(
                new MeshComponentId(meshObject.Id, componentType, slot)
            )
        );

        AssertThat(application.TryDeleteSelection()).IsNull();

        AssertThat(mesh.FaceCount).IsEqual(0);
        AssertThat(mesh.VertexCount).IsEqual(0);
        AssertBool(application.Selection.Current.IsEmpty).IsTrue();
        AssertThat(application.UndoDisplayName).IsEqual("Delete");

        AssertBool(application.Undo()).IsTrue();
        AssertThat(mesh.FaceCount).IsEqual(1);
        AssertThat(mesh.VertexCount).IsEqual(4);
        AssertThat(application.Selection.Current.Components.Count).IsEqual(1);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DeleteKeybindingIsRegisteredByDefault()
    {
        using var keybindings = new KeybindingManager("user://delete_keybind_test.cfg");
        keybindings.Load();
        try
        {
            InputEventKey? binding = keybindings.GetBinding(EditorInputActions.EditDelete);
            AssertThat(binding).IsNotNull();
            AssertThat(binding!.PhysicalKeycode).IsEqual(Key.Delete);
            AssertBool(InputMap.HasAction(EditorInputActions.EditDelete)).IsTrue();
        }
        finally
        {
            keybindings.ResetDefaults();
            if (Godot.FileAccess.FileExists("user://delete_keybind_test.cfg"))
            {
                DirAccess.RemoveAbsolute(
                    ProjectSettings.GlobalizePath("user://delete_keybind_test.cfg")
                );
            }
        }
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
            [
                NumericsVector2.Zero,
                NumericsVector2.UnitX,
                NumericsVector2.One,
                NumericsVector2.UnitY,
            ]
        );
        return mesh;
    }
}
