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
        Vector2 vertexScreen = viewport.Camera.UnprojectPosition(new Vector3(1.0f, 1.0f, 1.0f));
        application.HandleToolMouseDown(new EditorMouseInput(viewport.Camera, vertexScreen));

        AssertBool(application.Selection.Current.ObjectId is null).IsTrue();
        AssertBool(application.Selection.Current.Component.HasValue).IsTrue();
        MeshComponentId selected = application.Selection.Current.Component!.Value;
        AssertThat(selected.ObjectId).IsEqual(meshObject.Id);
        AssertThat(selected.Type).IsEqual(MeshComponentType.Vertex);
        AssertThat(application.UndoDisplayName).IsEqual("Select Component");

        AssertBool(application.Undo()).IsTrue();
        AssertBool(application.Selection.Current.IsEmpty).IsTrue();
        AssertBool(application.Redo()).IsTrue();
        AssertThat(application.Selection.Current.Component!.Value).IsEqual(selected);
    }
}
