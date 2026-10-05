using System;
using System.Threading.Tasks;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace TREditor.GodotTests;

[TestSuite]
public partial class ObjectTransformToolTests
{
    private static readonly Vector2 ViewportSize = new(640.0f, 480.0f);

    [TestCase]
    [RequireGodotRuntime]
    public async Task RotateAndScaleToolsUseTheSharedGizmoAndCommitUndoableTransforms()
    {
        using Fixture fixture = await Fixture.Create();
        Transform3D initial = fixture.Transform;

        await fixture.DragTransform(
            RotateTool.ToolId,
            TransformOperation.Rotate,
            expectBasisChange: true
        );
        AssertThat(fixture.Application.UndoDisplayName).IsEqual("Rotate");
        AssertBool(fixture.Application.Undo()).IsTrue();
        AssertThat(fixture.Transform).IsEqual(initial);
        AssertBool(fixture.Application.Redo()).IsTrue();
        AssertBool(fixture.Transform.Basis.IsEqualApprox(Basis.Identity)).IsFalse();

        Transform3D rotated = fixture.Transform;
        await fixture.DragTransform(
            ScaleTool.ToolId,
            TransformOperation.Scale,
            expectBasisChange: true
        );
        AssertThat(fixture.Application.UndoDisplayName).IsEqual("Scale");
        AssertBool(fixture.Application.Undo()).IsTrue();
        AssertThat(fixture.Transform).IsEqual(rotated);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void AngleSnapQuantizesGizmoRotationVectors()
    {
        var snap = new SnapSettings();
        snap.SetAngleSnap(15.0f);
        TestGizmoHost host = AutoFree(new TestGizmoHost(snap));

        Vector3 snapped = host.SnapRotation(Vector3.Up * Mathf.DegToRad(22.0f));

        AssertBool(Mathf.IsEqualApprox(Mathf.RadToDeg(snapped.Length()), 15.0f)).IsTrue();
        AssertThat(snapped.Normalized()).IsEqual(Vector3.Up);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void GridSnapQuantizesGizmoScaleDeltas()
    {
        var snap = new SnapSettings();
        snap.SetGridSnap(2.0f);
        TestGizmoHost host = AutoFree(new TestGizmoHost(snap));

        Vector3 snapped = host.SnapScale(new Vector3(1.2f, -3.1f, 4.9f));

        AssertThat(snapped).IsEqual(new Vector3(2.0f, -4.0f, 4.0f));
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DefaultToolShortcutsActivateTranslateRotateAndScale()
    {
        PackedScene rootScene = ResourceLoader.Load<PackedScene>("res://src/ui/Root.tscn");
        Root root = AutoFree(rootScene.Instantiate<Root>());
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(root);

        AssertShortcut(root, Key.R, RotateTool.ToolId);
        AssertShortcut(root, Key.Y, ScaleTool.ToolId);
        AssertShortcut(root, Key.T, TranslateTool.ToolId);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ReboundToolShortcutActivatesTheTool()
    {
        PackedScene rootScene = ResourceLoader.Load<PackedScene>("res://src/ui/Root.tscn");
        Root root = AutoFree(rootScene.Instantiate<Root>());
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(root);
        using var binding = new InputEventKey { PhysicalKeycode = Key.I };

        try
        {
            root.CurrentKeybindings!.Assign(EditorInputActions.ToolRotate, binding);
            AssertShortcut(root, Key.I, RotateTool.ToolId);
        }
        finally
        {
            root.CurrentKeybindings!.ResetDefaults();
        }
    }

    private static void AssertShortcut(Root root, Key key, string expectedToolId)
    {
        using var input = new InputEventKey { PhysicalKeycode = key, Pressed = true };
        root._UnhandledKeyInput(input);
        AssertThat(root.CurrentApplication!.Tools.ActiveTool?.Id).IsEqual(expectedToolId);
    }

    private static async Task ProcessFrames(int count)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        for (int index = 0; index < count; index++)
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly MainViewport _viewport;
        private readonly MeshObjectId _objectId;

        private Fixture(Application application, MainViewport viewport, MeshObjectId objectId)
        {
            Application = application;
            _viewport = viewport;
            _objectId = objectId;
        }

        public Application Application { get; }

        public Transform3D Transform
        {
            get
            {
                AssertBool(
                        Application.Document!.TryGetMeshTransform(
                            _objectId,
                            out Transform3D transform
                        )
                    )
                    .IsTrue();
                return transform;
            }
        }

        public static async Task<Fixture> Create()
        {
            var tree = (SceneTree)Engine.GetMainLoop();
            Node3D mapRoot = AutoFree(new Node3D());
            tree.Root.AddChild(mapRoot);
            var application = new Application(mapRoot);
            application.Snap.SetGridSnap(null);
            MeshObjectId objectId = TestSceneObjects.AddCube(application.CurrentDocument!);
            MainViewport viewport = AutoFree(
                new MainViewport { Position = Vector2.Zero, Size = ViewportSize }
            );
            tree.Root.AddChild(viewport);
            viewport.Camera.LookAtFromPosition(
                new Vector3(0.0f, 6.0f, 12.0f),
                Vector3.Zero,
                Vector3.Up
            );
            application.RegisterToolViewport(viewport.Camera, viewport.ToolOverlayRoot);
            application.Selection.Select(objectId);
            application.Update(0.0);
            await ProcessFrames(3);
            return new Fixture(application, viewport, objectId);
        }

        public async Task DragTransform(
            string toolId,
            TransformOperation operation,
            bool expectBasisChange
        )
        {
            Application.Tools.Activate(toolId);
            await ProcessFrames(2);
            AssertThat(Application.Gizmos.Mode).IsEqual(operation);
            Transform3D start = Transform;
            Vector2 center = _viewport.Camera.UnprojectPosition(
                Application.Gizmos.ProxyTransform.Origin
            );
            Vector2 handle = FindHandle(center);
            Vector2 radial = (handle - center).Normalized();
            Vector2 drag = operation == TransformOperation.Rotate
                ? new Vector2(-radial.Y, radial.X) * 50.0f
                : radial * 50.0f;

            Application.HandleToolMouseDown(new EditorMouseInput(_viewport.Camera, handle));
            Application.HandleToolMouseDrag(
                new EditorMouseInput(_viewport.Camera, handle + drag)
            );
            await ProcessFrames(1);
            Application.HandleToolMouseUp(
                new EditorMouseInput(_viewport.Camera, handle + drag)
            );

            AssertBool(Transform.Basis.IsEqualApprox(start.Basis)).IsEqual(!expectBasisChange);
        }

        public void Dispose() => Application.Dispose();

        private Vector2 FindHandle(Vector2 center)
        {
            for (int radius = 20; radius <= 130; radius += 4)
            {
                for (int degrees = 0; degrees < 360; degrees += 10)
                {
                    float angle = Mathf.DegToRad(degrees);
                    Vector2 candidate = center
                        + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    if (!Application.Gizmos.BeginInteraction(_viewport.Camera, candidate))
                        continue;

                    Application.Gizmos.Cancel();
                    return candidate;
                }
            }

            AssertThat("a transform handle was found").IsEqual("no handle was hit");
            return Vector2.Zero;
        }
    }

    private sealed partial class TestGizmoHost(SnapSettings snap) : TranslateGizmoHost(snap)
    {
        public Vector3 SnapRotation(Vector3 rotation) => EditRotate(rotation);

        public Vector3 SnapScale(Vector3 scale) => EditScale(scale);
    }
}
