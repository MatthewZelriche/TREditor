using System;
using System.Threading.Tasks;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace TREditor.GodotTests;

[TestSuite]
public class TranslateToolTests
{
    private static readonly Vector2 ViewportSize = new(640.0f, 480.0f);
    private static readonly Vector3 CameraPosition = new(0.0f, 6.0f, 12.0f);

    [TestCase]
    [RequireGodotRuntime]
    public async Task PressingTheGizmoDragsAndCommitsOneTranslate()
    {
        using Fixture fixture = await Fixture.Create();
        fixture.Select(fixture.CubeId);
        await ProcessFrames(3);
        Transform3D start = fixture.TransformOf(fixture.CubeId);
        Vector2 handle = fixture.FindHandle();

        fixture.MouseDown(handle);
        AssertBool(fixture.Application.Gizmos.IsInteracting).IsTrue();
        AssertThat(fixture.HistoryChanges).IsEqual(0);

        fixture.MouseDrag(handle + new Vector2(70.0f, 0.0f));
        await ProcessFrames(1);

        AssertBool(fixture.Preview.TryGetTransform(fixture.CubeId, out Transform3D preview))
            .IsTrue();
        AssertBool(preview.Origin.IsEqualApprox(start.Origin)).IsFalse();
        // A drag is feedback only; the document keeps the committed transform until mouse up.
        AssertThat(fixture.TransformOf(fixture.CubeId)).IsEqual(start);
        AssertThat(fixture.HistoryChanges).IsEqual(0);

        fixture.MouseUp(handle + new Vector2(70.0f, 0.0f));

        AssertThat(fixture.HistoryChanges).IsEqual(1);
        AssertThat(fixture.Application.UndoDisplayName).IsEqual("Translate");
        AssertThat(fixture.TransformOf(fixture.CubeId)).IsEqual(preview);
        AssertBool(fixture.Preview.TryGetTransform(fixture.CubeId, out Transform3D _)).IsFalse();
        AssertBool(fixture.Application.Gizmos.IsInteracting).IsFalse();

        AssertBool(fixture.Application.Undo()).IsTrue();
        AssertThat(fixture.TransformOf(fixture.CubeId)).IsEqual(start);
        AssertBool(fixture.Application.Redo()).IsTrue();
        AssertThat(fixture.TransformOf(fixture.CubeId)).IsEqual(preview);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task GridSnapQuantizesTranslationDeltas()
    {
        using Fixture fixture = await Fixture.Create();
        fixture.Application.Snap.SetGridSnap(2.0f);
        fixture.Select(fixture.CubeId);
        await ProcessFrames(3);
        Transform3D start = fixture.TransformOf(fixture.CubeId);
        Vector2 handle = fixture.FindHandle();

        fixture.MouseDown(handle);
        fixture.MouseDrag(handle + new Vector2(70.0f, 0.0f));
        await ProcessFrames(1);
        fixture.MouseUp(handle + new Vector2(70.0f, 0.0f));

        Transform3D after = fixture.TransformOf(fixture.CubeId);
        Vector3 delta = after.Origin - start.Origin;
        AssertBool(delta.IsEqualApprox(Vector3.Zero)).IsFalse();
        AssertBool(Mathf.IsEqualApprox(delta.X, Mathf.Snapped(delta.X, 2.0f))).IsTrue();
        AssertBool(Mathf.IsEqualApprox(delta.Y, Mathf.Snapped(delta.Y, 2.0f))).IsTrue();
        AssertBool(Mathf.IsEqualApprox(delta.Z, Mathf.Snapped(delta.Z, 2.0f))).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task APressOffTheGizmoChangesSelectionWithoutTranslating()
    {
        using Fixture fixture = await Fixture.Create();
        fixture.Select(fixture.CubeId);
        await ProcessFrames(3);
        Transform3D cubeStart = fixture.TransformOf(fixture.CubeId);

        fixture.MouseDown(fixture.ScreenPositionOf(fixture.CylinderId));

        AssertBool(fixture.Selection.Current.ObjectIds.Contains(fixture.CylinderId)).IsTrue();
        AssertThat(fixture.HistoryChanges).IsEqual(1);
        AssertThat(fixture.Application.UndoDisplayName).IsEqual("Select Object");
        AssertBool(fixture.Application.Gizmos.IsInteracting).IsFalse();

        // The press that changed the selection never becomes a drag.
        await ProcessFrames(2);
        fixture.MouseDrag(fixture.ScreenPositionOf(fixture.CylinderId) + new Vector2(70.0f, 0.0f));
        fixture.MouseUp(fixture.ScreenPositionOf(fixture.CylinderId) + new Vector2(70.0f, 0.0f));

        AssertThat(fixture.HistoryChanges).IsEqual(1);
        AssertThat(fixture.TransformOf(fixture.CubeId)).IsEqual(cubeStart);
        AssertBool(fixture.Preview.TryGetTransform(fixture.CylinderId, out Transform3D _)).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task ShiftClickTogglesObjectsAndPlacesTheGizmoAtTheirCentroid()
    {
        using Fixture fixture = await Fixture.Create();
        fixture.MouseDown(fixture.ScreenPositionOf(fixture.CylinderId));
        await ProcessFrames(2);

        fixture.MouseDown(fixture.ScreenPositionOf(fixture.CubeId), shiftPressed: true);

        AssertThat(fixture.Selection.Current.ObjectIds.Count).IsEqual(2);
        AssertBool(fixture.Selection.Current.ObjectIds.Contains(fixture.CubeId)).IsTrue();
        AssertBool(fixture.Selection.Current.ObjectIds.Contains(fixture.CylinderId)).IsTrue();
        Vector3 expectedCentroid = (
            fixture.TransformOf(fixture.CubeId).Origin
            + fixture.TransformOf(fixture.CylinderId).Origin
        ) / 2.0f;
        AssertBool(fixture.Application.Gizmos.IsVisible).IsTrue();
        AssertThat(fixture.Application.Gizmos.ProxyTransform.Origin).IsEqual(expectedCentroid);
        AssertThat(fixture.Application.UndoDisplayName).IsEqual("Select Object");

        fixture.MouseDown(fixture.ScreenPositionOf(fixture.CylinderId), shiftPressed: true);

        AssertThat(fixture.Selection.Current.ObjectIds.Count).IsEqual(1);
        AssertBool(fixture.Selection.Current.ObjectIds.Contains(fixture.CubeId)).IsTrue();
        AssertThat(fixture.Application.UndoDisplayName).IsEqual("Deselect Object");

        int changesBeforeEmptyClick = fixture.HistoryChanges;
        fixture.MouseDown(new Vector2(4.0f, 4.0f), shiftPressed: true);
        AssertThat(fixture.HistoryChanges).IsEqual(changesBeforeEmptyClick);
        AssertBool(fixture.Selection.Current.ObjectIds.Contains(fixture.CubeId)).IsTrue();

        AssertBool(fixture.Application.Undo()).IsTrue();
        AssertThat(fixture.Selection.Current.ObjectIds.Count).IsEqual(2);
        AssertBool(fixture.Application.Redo()).IsTrue();
        AssertBool(fixture.Selection.Current.ObjectIds.Contains(fixture.CubeId)).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task CentroidGizmoTranslatesEverySelectedObjectAsOneCommand()
    {
        using Fixture fixture = await Fixture.Create();
        fixture.MouseDown(fixture.ScreenPositionOf(fixture.CylinderId));
        await ProcessFrames(2);
        fixture.MouseDown(fixture.ScreenPositionOf(fixture.CubeId), shiftPressed: true);
        await ProcessFrames(3);
        Transform3D cubeStart = fixture.TransformOf(fixture.CubeId);
        Transform3D cylinderStart = fixture.TransformOf(fixture.CylinderId);
        Vector2 handle = fixture.FindHandle();

        fixture.MouseDown(handle);
        fixture.MouseDrag(handle + new Vector2(70.0f, 0.0f));
        await ProcessFrames(1);

        AssertBool(fixture.Preview.TryGetTransform(fixture.CubeId, out Transform3D cubePreview))
            .IsTrue();
        AssertBool(
                fixture.Preview.TryGetTransform(
                    fixture.CylinderId,
                    out Transform3D cylinderPreview
                )
            )
            .IsTrue();
        Vector3 cubeDelta = cubePreview.Origin - cubeStart.Origin;
        Vector3 cylinderDelta = cylinderPreview.Origin - cylinderStart.Origin;
        AssertBool(cubeDelta.IsZeroApprox()).IsFalse();
        AssertBool(cylinderDelta.IsEqualApprox(cubeDelta)).IsTrue();

        fixture.MouseUp(handle + new Vector2(70.0f, 0.0f));

        AssertThat(fixture.TransformOf(fixture.CubeId)).IsEqual(cubePreview);
        AssertThat(fixture.TransformOf(fixture.CylinderId)).IsEqual(cylinderPreview);
        AssertThat(fixture.Application.UndoDisplayName).IsEqual("Translate");
        AssertBool(fixture.Application.Undo()).IsTrue();
        AssertThat(fixture.TransformOf(fixture.CubeId)).IsEqual(cubeStart);
        AssertThat(fixture.TransformOf(fixture.CylinderId)).IsEqual(cylinderStart);
        AssertBool(fixture.Application.Redo()).IsTrue();
        AssertThat(fixture.TransformOf(fixture.CubeId)).IsEqual(cubePreview);
        AssertThat(fixture.TransformOf(fixture.CylinderId)).IsEqual(cylinderPreview);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task PressingTheSameObjectAgainAndEmptySpaceOnlyCommitsRealChanges()
    {
        using Fixture fixture = await Fixture.Create();
        Vector2 cylinder = fixture.ScreenPositionOf(fixture.CylinderId);

        fixture.MouseDown(cylinder);
        AssertBool(fixture.Selection.Current.ObjectIds.Contains(fixture.CylinderId)).IsTrue();
        AssertThat(fixture.HistoryChanges).IsEqual(1);

        // Re-picking the current selection is not a change, so it produces no command.
        await ProcessFrames(2);
        fixture.MouseDown(cylinder);
        AssertThat(fixture.HistoryChanges).IsEqual(1);

        fixture.MouseDown(new Vector2(4.0f, 4.0f));
        AssertThat(fixture.Selection.Current.ObjectIds.Count).IsEqual(0);
        AssertThat(fixture.HistoryChanges).IsEqual(2);
        AssertThat(fixture.Application.UndoDisplayName).IsEqual("Clear Selection");

        // Missing the scene with nothing selected clears nothing.
        fixture.MouseDown(new Vector2(4.0f, 4.0f));
        AssertThat(fixture.HistoryChanges).IsEqual(2);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task DragsFromAnotherCameraAndZeroMovementCommitNothing()
    {
        using Fixture fixture = await Fixture.Create();
        MainViewport other = CreateViewport();
        fixture.Application.RegisterToolViewport(other.Camera, other.ToolOverlayRoot);
        fixture.Select(fixture.CubeId);
        await ProcessFrames(3);
        Transform3D start = fixture.TransformOf(fixture.CubeId);
        Vector2 handle = fixture.FindHandle();
        fixture.MouseDown(handle);

        fixture.Tool.OnMouseDrag(
            fixture.Context,
            new EditorMouseInput(other.Camera, handle + new Vector2(70.0f, 0.0f))
        );
        fixture.Tool.OnMouseUp(
            fixture.Context,
            new EditorMouseInput(other.Camera, handle + new Vector2(70.0f, 0.0f))
        );

        AssertBool(fixture.Preview.TryGetTransform(fixture.CubeId, out Transform3D _)).IsFalse();
        AssertBool(fixture.Application.Gizmos.IsInteracting).IsTrue();

        // Releasing where the drag started moved nothing, so there is nothing to record.
        fixture.MouseUp(handle);

        AssertThat(fixture.HistoryChanges).IsEqual(0);
        AssertThat(fixture.TransformOf(fixture.CubeId)).IsEqual(start);
        AssertBool(fixture.Application.Gizmos.IsInteracting).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task RepeatedPreviewsAreDiscardedByCancel()
    {
        using Fixture fixture = await Fixture.Create();
        fixture.Select(fixture.CubeId);
        await ProcessFrames(3);
        Transform3D start = fixture.TransformOf(fixture.CubeId);
        Vector2 handle = fixture.FindHandle();

        fixture.MouseDown(handle);
        fixture.MouseDrag(handle + new Vector2(30.0f, 0.0f));
        fixture.MouseDrag(handle + new Vector2(60.0f, 0.0f));
        await ProcessFrames(1);
        AssertBool(fixture.Preview.TryGetTransform(fixture.CubeId, out Transform3D _)).IsTrue();

        fixture.Application.CancelActiveInteraction();

        AssertThat(fixture.HistoryChanges).IsEqual(0);
        AssertBool(fixture.Selection.Current.ObjectIds.Contains(fixture.CubeId)).IsTrue();
        AssertThat(fixture.TransformOf(fixture.CubeId)).IsEqual(start);
        AssertBool(fixture.Preview.TryGetTransform(fixture.CubeId, out Transform3D _)).IsFalse();
        AssertBool(fixture.Application.Gizmos.IsInteracting).IsFalse();
        // The gizmo returns to the object it is still attached to.
        AssertThat(fixture.Application.Gizmos.ProxyTransform.Origin).IsEqual(start.Origin);

        // Cancelling again while idle is harmless, and so is a stray drag afterwards.
        fixture.Application.CancelActiveInteraction();
        fixture.MouseDrag(handle + new Vector2(90.0f, 0.0f));
        AssertBool(fixture.Preview.TryGetTransform(fixture.CubeId, out Transform3D _)).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task AFailedCommandLeavesNoPreviewOrInteractionBehind()
    {
        using Fixture fixture = await Fixture.Create();
        fixture.Select(fixture.CubeId);
        await ProcessFrames(3);
        Transform3D start = fixture.TransformOf(fixture.CubeId);
        Vector2 handle = fixture.FindHandle();
        var failing = new FailingCommandContext(fixture.Context);

        fixture.Tool.OnMouseDown(failing, new EditorMouseInput(fixture.Camera, handle));
        fixture.Tool.OnMouseDrag(
            failing,
            new EditorMouseInput(fixture.Camera, handle + new Vector2(70.0f, 0.0f))
        );
        await ProcessFrames(1);

        AssertThrown(
                () =>
                    fixture.Tool.OnMouseUp(
                        failing,
                        new EditorMouseInput(fixture.Camera, handle + new Vector2(70.0f, 0.0f))
                    )
            )
            .IsInstanceOf<InvalidOperationException>();

        AssertThat(fixture.HistoryChanges).IsEqual(0);
        AssertThat(fixture.TransformOf(fixture.CubeId)).IsEqual(start);
        AssertBool(fixture.Preview.TryGetTransform(fixture.CubeId, out Transform3D _)).IsFalse();
        AssertBool(fixture.Application.Gizmos.IsInteracting).IsFalse();
        AssertThat(fixture.Application.Gizmos.ProxyTransform.Origin).IsEqual(start.Origin);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task AStaleCapturedRecordFailsOnMouseUpWithoutCommitting()
    {
        using Fixture fixture = await Fixture.Create();
        fixture.Select(fixture.CubeId);
        await ProcessFrames(3);
        Transform3D start = fixture.TransformOf(fixture.CubeId);
        Vector2 handle = fixture.FindHandle();
        int operationFailedCount = 0;
        fixture.Application.OperationFailed += (_, _) => operationFailedCount++;

        fixture.MouseDown(handle);
        fixture.MouseDrag(handle + new Vector2(70.0f, 0.0f));
        await ProcessFrames(1);
        AssertBool(fixture.Preview.TryGetTransform(fixture.CubeId, out Transform3D preview))
            .IsTrue();
        AssertBool(preview.Origin.IsEqualApprox(start.Origin)).IsFalse();

        AssertBool(fixture.Application.Document!.TryGetObject(fixture.CubeId, out MeshObject current))
            .IsTrue();
        var intervening = current with
        {
            Transform = new Transform3D(start.Basis, start.Origin + new Vector3(0.0f, 3.0f, 0.0f)),
        };
        AssertBool(fixture.Application.CurrentDocument!.ReplaceObject(current, intervening)).IsTrue();
        int historyBeforeUp = fixture.HistoryChanges;

        fixture.MouseUp(handle + new Vector2(70.0f, 0.0f));

        AssertThat(operationFailedCount).IsEqual(1);
        AssertThat(fixture.HistoryChanges).IsEqual(historyBeforeUp);
        AssertThat(fixture.TransformOf(fixture.CubeId)).IsEqual(intervening.Transform);
        AssertBool(fixture.Preview.TryGetTransform(fixture.CubeId, out Transform3D _)).IsFalse();
        AssertBool(fixture.Application.Gizmos.IsInteracting).IsFalse();
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

    private static async Task ProcessFrames(int count)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        for (int index = 0; index < count; index++)
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    // Everything a translate test needs: a running application with a seeded cube and cylinder, one
    // registered viewport, and the tool under test driven exactly the way the viewport drives it.
    private sealed class Fixture : IDisposable
    {
        private readonly MainViewport _viewport;

        private Fixture(
            Application application,
            MainViewport viewport,
            MeshObjectId cubeId,
            MeshObjectId cylinderId
        )
        {
            Application = application;
            _viewport = viewport;
            CubeId = cubeId;
            CylinderId = cylinderId;
            application.CommandHistoryChanged += () => HistoryChanges++;
        }

        public Application Application { get; }

        public TranslateTool Tool => (TranslateTool)Application.Tools.ActiveTool!;

        public IEditorToolContext Context => Application.ToolContext;

        public PreviewState Preview => Application.ToolContext.Preview;

        public SelectionState Selection => Application.Selection;

        public Camera3D Camera => _viewport.Camera;

        public MeshObjectId CubeId { get; }

        public MeshObjectId CylinderId { get; }

        public int HistoryChanges { get; private set; }

        public static async Task<Fixture> Create()
        {
            var tree = (SceneTree)Engine.GetMainLoop();
            Node3D mapRoot = AutoFree(new Node3D());
            tree.Root.AddChild(mapRoot);
            var application = new Application(mapRoot);
            // Freehand translate tests need snap off; the dedicated snap case turns it back on.
            application.Snap.SetGridSnap(null);
            (MeshObjectId cubeId, MeshObjectId cylinderId) = TestSceneObjects.AddCubeAndCylinder(
                application.CurrentDocument!
            );
            MainViewport viewport = CreateViewport();
            application.RegisterToolViewport(viewport.Camera, viewport.ToolOverlayRoot);

            // Colliders only exist after a flush, and the ray they answer needs a physics frame.
            application.Update(0.0);
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            return new Fixture(application, viewport, cubeId, cylinderId);
        }

        public void Select(MeshObjectId id) => Selection.Select(id);

        public Transform3D TransformOf(MeshObjectId id)
        {
            AssertBool(Application.Document!.TryGetMeshTransform(id, out Transform3D transform))
                .IsTrue();
            return transform;
        }

        public Vector2 ScreenPositionOf(MeshObjectId id) =>
            Camera.UnprojectPosition(TransformOf(id).Origin);

        public void MouseDown(Vector2 position) =>
            Application.HandleToolMouseDown(new EditorMouseInput(Camera, position));

        public void MouseDown(Vector2 position, bool shiftPressed) =>
            Application.HandleToolMouseDown(
                new EditorMouseInput(Camera, position, IsShiftPressed: shiftPressed)
            );

        public void MouseDrag(Vector2 position) =>
            Application.HandleToolMouseDrag(new EditorMouseInput(Camera, position));

        public void MouseUp(Vector2 position) =>
            Application.HandleToolMouseUp(new EditorMouseInput(Camera, position));

        // Walks outward along the projected +X axis from the gizmo until a handle answers a press.
        public Vector2 FindHandle()
        {
            Vector3 origin = Application.Gizmos.ProxyTransform.Origin;
            for (int step = 1; step <= 60; step++)
            {
                Vector2 candidate = HandlePosition(origin, 4.0f * step);
                if (Application.Gizmos.BeginInteraction(Camera, candidate))
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
            Vector2 center = Camera.UnprojectPosition(origin);
            Vector2 axis = (Camera.UnprojectPosition(origin + Vector3.Right) - center).Normalized();
            return center + (axis * offset);
        }
    }

    // Stands in for the real command sink so a failing commit can be observed without corrupting the
    // application's history.
    private sealed class FailingCommandContext(IEditorToolContext inner)
        : IEditorToolContext,
            IEditorCommandSink
    {
        public IEditorDocumentReader Document => inner.Document;

        public IEditorCommandSink Commands => this;

        public PickingService Picking => inner.Picking;

        public IEditorSelection Selection => inner.Selection;

        public PreviewState Preview => inner.Preview;

        public TranslateGizmoCoordinator TranslateGizmos => inner.TranslateGizmos;

        public SnapSettings Snap => inner.Snap;

        public void Submit(IEditorCommand command) =>
            throw new InvalidOperationException("The command could not be submitted.");
    }
}
