using System.Threading.Tasks;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace TREditor.GodotTests;

[TestSuite]
public class TranslateVerticalSliceTests
{
    private static readonly Vector2 ViewportSize = new(640.0f, 480.0f);
    private static readonly Vector3 CameraPosition = new(0.0f, 6.0f, 12.0f);

    [TestCase]
    [RequireGodotRuntime]
    public async Task TheTranslateToolCompletesThePreviewCommitAndReactFlow()
    {
        using Fixture fixture = await Fixture.Create();
        Transform3D cubeStart = fixture.TransformOf(fixture.CubeId);
        Vector2 cubeScreen = fixture.ScreenPositionOf(fixture.CubeId);
        int selectionChangedCount = 0;
        fixture.Application.Selection.Changed += () => selectionChangedCount++;

        // A scene click selects without starting translation.
        fixture.MouseDown(cubeScreen);
        AssertThat(fixture.Application.Selection.Selected).IsEqual(fixture.CubeId);
        AssertThat(fixture.Application.CanUndo).IsTrue();
        AssertThat(fixture.Application.UndoDisplayName).IsEqual("Select Object");
        AssertBool(fixture.Application.Gizmos.IsInteracting).IsFalse();
        await ProcessFrames(2);

        AssertBool(fixture.Application.Undo()).IsTrue();
        AssertThat(fixture.Application.Selection.Selected).IsNull();
        AssertBool(fixture.Application.Redo()).IsTrue();
        AssertThat(fixture.Application.Selection.Selected).IsEqual(fixture.CubeId);
        AssertThat(selectionChangedCount).IsEqual(3);

        await ProcessFrames(3);
        Vector2 handle = fixture.FindHandle();
        fixture.MouseDown(handle);
        AssertBool(fixture.Application.Gizmos.IsInteracting).IsTrue();
        AssertThat(fixture.Application.CanUndo).IsTrue();
        AssertThat(fixture.Application.UndoDisplayName).IsEqual("Select Object");

        fixture.MouseDrag(handle + new Vector2(80.0f, 0.0f));
        await ProcessFrames(1);

        AssertBool(fixture.Preview.TryGetTransform(fixture.CubeId, out Transform3D preview))
            .IsTrue();
        AssertBool(preview.Origin.IsEqualApprox(cubeStart.Origin)).IsFalse();
        AssertThat(fixture.TransformOf(fixture.CubeId)).IsEqual(cubeStart);
        AssertThat(fixture.RenderedTransformOf(fixture.CubeId)).IsEqual(preview);

        fixture.MouseUp(handle + new Vector2(80.0f, 0.0f));

        AssertThat(fixture.Application.CanUndo).IsTrue();
        AssertThat(fixture.Application.UndoDisplayName).IsEqual("Translate");
        AssertThat(fixture.TransformOf(fixture.CubeId)).IsEqual(preview);
        AssertThat(fixture.RenderedTransformOf(fixture.CubeId)).IsEqual(preview);
        AssertBool(fixture.Preview.TryGetTransform(fixture.CubeId, out Transform3D _)).IsFalse();
        AssertThat(fixture.RaycastObjectAt(preview.Origin)).IsEqual(fixture.CubeId);

        AssertBool(fixture.Application.Undo()).IsTrue();
        AssertThat(fixture.TransformOf(fixture.CubeId)).IsEqual(cubeStart);
        AssertThat(fixture.RenderedTransformOf(fixture.CubeId)).IsEqual(cubeStart);

        AssertBool(fixture.Application.Redo()).IsTrue();
        AssertThat(fixture.TransformOf(fixture.CubeId)).IsEqual(preview);
        AssertThat(fixture.RenderedTransformOf(fixture.CubeId)).IsEqual(preview);

        // Cancellation abandons the drag without recording history.
        fixture.MouseDown(handle);
        fixture.MouseDrag(handle + new Vector2(40.0f, 0.0f));
        await ProcessFrames(1);
        AssertBool(fixture.Preview.TryGetTransform(fixture.CubeId, out Transform3D _)).IsTrue();
        AssertThat(fixture.Application.CanUndo).IsTrue();
        AssertThat(fixture.Application.UndoDisplayName).IsEqual("Translate");

        fixture.Application.CancelActiveInteraction();

        AssertBool(fixture.Preview.TryGetTransform(fixture.CubeId, out Transform3D _)).IsFalse();
        AssertThat(fixture.TransformOf(fixture.CubeId)).IsEqual(preview);
        AssertThat(fixture.Application.CanUndo).IsTrue();
        AssertThat(fixture.Application.UndoDisplayName).IsEqual("Translate");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void TranslateIsRegisteredAsTheDefaultTool()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);

        AssertThat(application.Tools.ActiveTool?.Id).IsEqual(TranslateTool.ToolId);

        var other = new RecordingTool("test.other");
        application.Tools.Register(other);
        application.Tools.Activate(other.Id);
        application.Tools.Deactivate();

        AssertThat(application.Tools.ActiveTool?.Id).IsEqual(TranslateTool.ToolId);
    }

    private sealed class RecordingTool(string id) : IEditorTool
    {
        public string Id => id;

        public void OnMouseDown(IEditorToolContext context, EditorMouseInput input) { }

        public void OnMouseDrag(IEditorToolContext context, EditorMouseInput input) { }

        public void OnMouseUp(IEditorToolContext context, EditorMouseInput input) { }

        public void Cancel(IEditorToolContext context) { }
    }

    private static async Task ProcessFrames(int count)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        for (int index = 0; index < count; index++)
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private sealed class Fixture : System.IDisposable
    {
        private readonly Node3D _mapRoot;
        private readonly MainViewport _viewport;

        private Fixture(Application application, Node3D mapRoot, MainViewport viewport)
        {
            Application = application;
            _mapRoot = mapRoot;
            _viewport = viewport;

            foreach (MeshObjectId id in application.Document!.GetObjectIds())
            {
                application.Document.TryGetMeshTransform(id, out Transform3D transform);
                if (transform.Origin.X < 0.0f)
                    CubeId = id;
                else
                    CylinderId = id;
            }
        }

        public Application Application { get; }

        public MeshObjectId CubeId { get; }

        public MeshObjectId CylinderId { get; }

        public PreviewState Preview => Application.ToolContext.Preview;

        public static async Task<Fixture> Create()
        {
            var tree = (SceneTree)Engine.GetMainLoop();
            Node3D mapRoot = AutoFree(new Node3D());
            tree.Root.AddChild(mapRoot);
            var application = new Application(mapRoot);
            MainViewport viewport = CreateViewport();
            application.RegisterToolViewport(viewport.Camera, viewport.ToolOverlayRoot);
            application.Update(0.0);
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            return new Fixture(application, mapRoot, viewport);
        }

        public Transform3D TransformOf(MeshObjectId id)
        {
            AssertBool(Application.Document!.TryGetMeshTransform(id, out Transform3D transform))
                .IsTrue();
            return transform;
        }

        public Transform3D RenderedTransformOf(MeshObjectId id) =>
            _mapRoot
                .GetNode<Node3D>("Render")
                .GetNode<MeshInstance3D>($"MeshObject_{id.Value}")
                .Transform;

        public Vector2 ScreenPositionOf(MeshObjectId id) =>
            _viewport.Camera.UnprojectPosition(TransformOf(id).Origin);

        public void MouseDown(Vector2 position) =>
            Application.HandleToolMouseDown(new EditorMouseInput(_viewport.Camera, position));

        public void MouseDrag(Vector2 position) =>
            Application.HandleToolMouseDrag(new EditorMouseInput(_viewport.Camera, position));

        public void MouseUp(Vector2 position) =>
            Application.HandleToolMouseUp(new EditorMouseInput(_viewport.Camera, position));

        public MeshObjectId? RaycastObjectAt(Vector3 worldPosition)
        {
            Vector3 origin = worldPosition + new Vector3(0.0f, 0.0f, 8.0f);
            Vector3 destination = worldPosition + new Vector3(0.0f, 0.0f, -8.0f);
            using PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(
                origin,
                destination
            );
            return Application.ToolContext.Picking.Raycast(query);
        }

        public Vector2 FindHandle()
        {
            Application.ToolContext.Selection.Select(CubeId);
            Vector3 origin = Application.Gizmos.ProxyTransform.Origin;
            for (int step = 1; step <= 60; step++)
            {
                Vector2 candidate = HandlePosition(origin, 4.0f * step);
                if (Application.Gizmos.BeginInteraction(_viewport.Camera, candidate))
                {
                    Application.Gizmos.Cancel();
                    return candidate;
                }
            }

            AssertThat("a handle was found").IsEqual("no handle was hit");
            return Vector2.Zero;
        }

        public void Dispose() => Application.Dispose();

        private Vector2 HandlePosition(Vector3 origin, float offset)
        {
            Vector2 center = _viewport.Camera.UnprojectPosition(origin);
            Vector2 axis = (
                _viewport.Camera.UnprojectPosition(origin + Vector3.Right) - center
            ).Normalized();
            return center + (axis * offset);
        }

        private static MainViewport CreateViewport()
        {
            MainViewport viewport = AutoFree(
                new MainViewport { Position = Vector2.Zero, Size = ViewportSize }
            );
            ((SceneTree)Engine.GetMainLoop()).Root.AddChild(viewport);
            viewport.Camera.LookAtFromPosition(CameraPosition, Vector3.Zero, Vector3.Up);
            return viewport;
        }
    }
}
