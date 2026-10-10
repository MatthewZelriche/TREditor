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
        application.Tools.Activate(MergeVerticesTool.ToolId);
        var tool = (MergeVerticesTool)application.Tools.ActiveTool!;

        AssertThat(application.Tools.TryExecutePrimaryAction()).IsNull();

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
        AssertThat(application.Tools.TryExecutePrimaryAction()).IsNull();
        AssertThat(mesh.GetVertexData(last).position).IsEqual(new NumericsVector3(1f, 0f, 0f));
        AssertBool(application.Undo()).IsTrue();

        tool.Position = VertexMergePosition.First;
        AssertThat(application.Tools.TryExecutePrimaryAction()).IsNull();
        AssertBool(mesh.IsVertexAlive(first)).IsTrue();
        AssertBool(mesh.IsVertexAlive(last)).IsFalse();
        AssertThat(mesh.GetVertexData(first).position).IsEqual(NumericsVector3.Zero);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void TopologyFailureKeepsActionEnabledAndShowsOnlyWarningToast()
    {
        PackedScene rootScene = ResourceLoader.Load<PackedScene>("res://src/ui/Root.tscn");
        Root root = AutoFree(rootScene.Instantiate<Root>());
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(root);
        Application application = root.CurrentApplication!;
        application.SelectionMode.ToggleComponent(ComponentMask.Vertex);
        application.Tools.Activate(MergeVerticesTool.ToolId);
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
        int operationFailureCount = 0;
        application.OperationFailed += (_, _) => operationFailureCount++;
        Button actionButton = root.GetNode<Button>(
            "UI/ToolSettingsSidebar/Panel/Margin/Contents/PrimaryActionRow/PrimaryActionButton"
        );

        AssertBool(actionButton.Disabled).IsFalse();
        actionButton.EmitSignal(BaseButton.SignalName.Pressed);

        PanelContainer toast = root.GetNode<PanelContainer>("UI/WarningToast");
        AssertBool(toast.Visible).IsTrue();
        AssertThat(toast.GetNode<Label>("WarningToastLabel").Text)
            .Contains("cannot be connected");
        AssertBool(root.GetNode<AcceptDialog>("%FileErrorDialog").Visible).IsFalse();
        AssertThat(operationFailureCount).IsEqual(0);
        AssertThat(mesh.VertexCount).IsEqual(6);
        AssertThat(mesh.FaceCount).IsEqual(2);
        AssertBool(application.CanUndo).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void SidebarActionTracksSelectionReadinessAndExplainsWhyItIsDisabled()
    {
        PackedScene rootScene = ResourceLoader.Load<PackedScene>("res://src/ui/Root.tscn");
        Root root = AutoFree(rootScene.Instantiate<Root>());
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(root);
        Application application = root.CurrentApplication!;
        application.SelectionMode.ToggleComponent(ComponentMask.Vertex);
        application.Tools.Activate(MergeVerticesTool.ToolId);

        ToolSettingsSidebar sidebar = root.GetNode<ToolSettingsSidebar>("UI/ToolSettingsSidebar");
        Button actionButton = sidebar.GetNode<Button>(
            "Panel/Margin/Contents/PrimaryActionRow/PrimaryActionButton"
        );
        Label warning = sidebar.GetNode<Label>(
            "Panel/Margin/Contents/PrimaryActionRow/PrimaryActionWarning"
        );

        AssertBool(actionButton.Disabled).IsTrue();
        AssertThat(actionButton.TooltipText)
            .IsEqual("Select at least two vertices from one mesh.");
        AssertBool(warning.Visible).IsTrue();
        AssertThat(warning.Text).IsEqual("\u26A0");
        AssertThat((int)warning.MouseFilter)
            .IsEqual((int)Control.MouseFilterEnum.Stop);
        AssertThat(warning.GetThemeColor("font_color")).IsEqual(new Color("e5b94b"));
        AssertThat(warning.TooltipText).IsEqual("Select at least two vertices from one mesh.");

        SpatialMesh mesh = CreateQuad(out int first, out int last);
        MeshObject meshObject = application.CurrentDocument!.Add(
            "Quad",
            Transform3D.Identity,
            new GDMesh(mesh)
        );
        application.Selection.Apply(
            SelectionValue.ForSelections(
                [],
                [
                    new(meshObject.Id, MeshComponentType.Vertex, first),
                    new(meshObject.Id, MeshComponentType.Edge, 0),
                ]
            )
        );

        AssertBool(actionButton.Disabled).IsTrue();
        AssertThat(warning.TooltipText).Contains("Only vertices can be merged");

        application.Selection.Apply(
            SelectionValue.ForSelections(
                [],
                [
                    new(meshObject.Id, MeshComponentType.Vertex, first),
                    new(meshObject.Id, MeshComponentType.Vertex, last),
                ]
            )
        );

        AssertBool(actionButton.Disabled).IsFalse();
        AssertThat(actionButton.TooltipText).IsEqual(string.Empty);
        AssertBool(warning.Visible).IsFalse();
        AssertThat(warning.TooltipText).IsEqual(string.Empty);
        actionButton.EmitSignal(BaseButton.SignalName.Pressed);
        AssertThat(mesh.VertexCount).IsEqual(3);
        AssertThat(application.UndoDisplayName).IsEqual("Merge Vertices");

        AssertBool(application.Undo()).IsTrue();
        using var enter = new InputEventKey
        {
            Keycode = Key.Enter,
            PhysicalKeycode = Key.Enter,
            Pressed = true,
        };
        root._UnhandledKeyInput(enter);
        AssertThat(mesh.VertexCount).IsEqual(3);
        AssertThat(application.UndoDisplayName).IsEqual("Merge Vertices");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void EnterOnAnUnavailablePrimaryActionShowsItsReasonInAWarningToast()
    {
        PackedScene rootScene = ResourceLoader.Load<PackedScene>("res://src/ui/Root.tscn");
        Root root = AutoFree(rootScene.Instantiate<Root>());
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(root);
        Application application = root.CurrentApplication!;
        application.SelectionMode.ToggleComponent(ComponentMask.Vertex);
        application.Tools.Activate(MergeVerticesTool.ToolId);
        using var enter = new InputEventKey
        {
            Keycode = Key.Enter,
            PhysicalKeycode = Key.Enter,
            Pressed = true,
        };

        root._UnhandledKeyInput(enter);

        PanelContainer toast = root.GetNode<PanelContainer>("UI/WarningToast");
        Label message = toast.GetNode<Label>("WarningToastLabel");
        AssertBool(toast.Visible).IsTrue();
        AssertThat(message.Text)
            .IsEqual("\u26A0 Select at least two vertices from one mesh.");
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
