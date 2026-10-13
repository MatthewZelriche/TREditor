using System.Threading.Tasks;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace TREditor.GodotTests;

[TestSuite]
public class BezierCurveGizmoTests
{
    private static readonly Vector2 ViewportSize = new(640.0f, 480.0f);

    [TestCase]
    [RequireGodotRuntime]
    public async Task ShowsCurveAnchorsAndTangentsInEveryRegisteredViewport()
    {
        using var curve = CreateCurve();
        using var gizmo = new BezierCurveGizmo();
        MainViewport first = CreateViewport(new Vector3(0.0f, 0.0f, 10.0f));
        MainViewport second = CreateViewport(new Vector3(8.0f, 6.0f, 8.0f));
        gizmo.RegisterViewport(first.Camera, first.ToolOverlayRoot, 1u << 20);
        gizmo.RegisterViewport(second.Camera, second.ToolOverlayRoot, 1u << 21);

        gizmo.Show(curve, Transform3D.Identity);
        await ProcessFrames(2);

        AssertBool(gizmo.IsVisible).IsTrue();
        AssertThat(gizmo.ViewportCount).IsEqual(2);
        Node3D firstHost = first.ToolOverlayRoot.GetNode<Node3D>("BezierCurveGizmo");
        Node3D secondHost = second.ToolOverlayRoot.GetNode<Node3D>("BezierCurveGizmo");
        AssertBool(firstHost.Visible).IsTrue();
        AssertBool(secondHost.Visible).IsTrue();
        AssertThat(
                firstHost.GetNode<MultiMeshInstance3D>("Anchors").Multimesh.InstanceCount
            )
            .IsEqual(3);
        AssertThat(
                firstHost.GetNode<MultiMeshInstance3D>("Tangents").Multimesh.InstanceCount
            )
            .IsEqual(4);
        AssertThat(firstHost.GetNode<MeshInstance3D>("Curve").Layers).IsEqual(1u << 20);
        AssertThat(secondHost.GetNode<MeshInstance3D>("Curve").Layers).IsEqual(1u << 21);

        gizmo.Hide();
        AssertBool(firstHost.Visible).IsFalse();
        AssertBool(secondHost.Visible).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task DraggingAnAnchorEditsTheBorrowedCurveAndRaisesLifecycleEvents()
    {
        using var curve = CreateCurve();
        using var gizmo = new BezierCurveGizmo();
        MainViewport viewport = CreateViewport(new Vector3(0.0f, 0.0f, 10.0f));
        gizmo.RegisterViewport(viewport.Camera, viewport.ToolOverlayRoot, 1u << 20);
        gizmo.Show(curve, Transform3D.Identity);
        await ProcessFrames(1);

        int started = 0;
        int changed = 0;
        int finished = 0;
        bool cancelled = true;
        gizmo.DragStarted += _ => started++;
        gizmo.PreviewChanged += () => changed++;
        gizmo.DragFinished += (_, wasCancelled) =>
        {
            finished++;
            cancelled = wasCancelled;
        };

        Vector3 start = curve.GetPointPosition(1);
        Vector3 target = start + new Vector3(0.75f, 1.25f, 0.0f);
        AssertBool(
                gizmo.BeginInteraction(viewport.Camera, viewport.Camera.UnprojectPosition(start))
            )
            .IsTrue();
        AssertBool(
                gizmo.UpdateInteraction(viewport.Camera, viewport.Camera.UnprojectPosition(target))
            )
            .IsTrue();
        gizmo.EndInteraction();

        AssertBool(curve.GetPointPosition(1).IsEqualApprox(target)).IsTrue();
        AssertThat(started).IsEqual(1);
        AssertThat(changed).IsEqual(1);
        AssertThat(finished).IsEqual(1);
        AssertBool(cancelled).IsFalse();
        AssertBool(gizmo.IsInteracting).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task CancellingADragRestoresTheCompleteCurveSnapshot()
    {
        using var curve = CreateCurve();
        using var gizmo = new BezierCurveGizmo();
        MainViewport viewport = CreateViewport(new Vector3(0.0f, 0.0f, 10.0f));
        gizmo.RegisterViewport(viewport.Camera, viewport.ToolOverlayRoot, 1u << 20);
        gizmo.Show(curve, Transform3D.Identity);
        await ProcessFrames(1);
        BezierCurveSnapshot before = BezierCurveSnapshot.Capture(curve);
        bool cancelled = false;
        gizmo.DragFinished += (_, wasCancelled) => cancelled = wasCancelled;

        Vector3 start = curve.GetPointPosition(1);
        gizmo.BeginInteraction(viewport.Camera, viewport.Camera.UnprojectPosition(start));
        gizmo.UpdateInteraction(
            viewport.Camera,
            viewport.Camera.UnprojectPosition(start + Vector3.Up * 2.0f)
        );
        gizmo.Cancel();

        AssertBool(cancelled).IsTrue();
        AssertCurveEquals(before, curve);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task MirroredAndAlignedModesCoupleTheOppositeTangent()
    {
        using var curve = CreateCurve();
        using var gizmo = new BezierCurveGizmo();
        MainViewport viewport = CreateViewport(new Vector3(0.0f, 0.0f, 10.0f));
        gizmo.RegisterViewport(viewport.Camera, viewport.ToolOverlayRoot, 1u << 20);
        gizmo.Show(curve, Transform3D.Identity);
        await ProcessFrames(1);
        var outgoing = new BezierCurveHandle(1, BezierCurveHandleKind.Outgoing);
        gizmo.SetHandleMode(1, BezierCurveHandleMode.Mirrored);

        Vector3 anchor = curve.GetPointPosition(1);
        Vector3 handleStart = anchor + curve.GetPointOut(1);
        Vector3 handleTarget = anchor + new Vector3(1.0f, 1.0f, 0.0f);
        gizmo.BeginInteraction(viewport.Camera, viewport.Camera.UnprojectPosition(handleStart));
        gizmo.UpdateInteraction(viewport.Camera, viewport.Camera.UnprojectPosition(handleTarget));
        gizmo.EndInteraction();

        AssertBool(
                curve.GetPointOut(1).IsEqualApprox(new Vector3(1.0f, 1.0f, 0.0f))
            )
            .IsTrue();
        AssertBool(
                curve.GetPointIn(1).IsEqualApprox(new Vector3(-1.0f, -1.0f, 0.0f))
            )
            .IsTrue();

        curve.SetPointIn(1, new Vector3(-2.0f, 0.0f, 0.0f));
        curve.SetPointOut(1, Vector3.Right);
        gizmo.SetHandleMode(1, BezierCurveHandleMode.Aligned);
        handleStart = anchor + curve.GetPointOut(1);
        handleTarget = anchor + Vector3.Up;
        gizmo.BeginInteraction(viewport.Camera, viewport.Camera.UnprojectPosition(handleStart));
        gizmo.UpdateInteraction(viewport.Camera, viewport.Camera.UnprojectPosition(handleTarget));
        gizmo.EndInteraction();

        AssertFloat(curve.GetPointIn(1).Length()).IsEqualApprox(2.0f, 0.001f);
        AssertBool(curve.GetPointIn(1).Normalized().IsEqualApprox(Vector3.Down)).IsTrue();
        AssertThat(gizmo.SelectedHandle).IsEqual(outgoing);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task LocksAndConsumerConstraintsControlMovement()
    {
        using var curve = CreateCurve();
        using var gizmo = new BezierCurveGizmo();
        MainViewport viewport = CreateViewport(new Vector3(0.0f, 0.0f, 10.0f));
        gizmo.RegisterViewport(viewport.Camera, viewport.ToolOverlayRoot, 1u << 20);
        gizmo.Show(curve, Transform3D.Identity);
        await ProcessFrames(1);
        var anchor = new BezierCurveHandle(1, BezierCurveHandleKind.Anchor);
        Vector3 start = curve.GetPointPosition(1);

        gizmo.SetHandleLocked(anchor, true);
        AssertBool(
                gizmo.BeginInteraction(viewport.Camera, viewport.Camera.UnprojectPosition(start))
            )
            .IsTrue();
        AssertBool(gizmo.IsInteracting).IsFalse();
        AssertBool(curve.GetPointPosition(1).IsEqualApprox(start)).IsTrue();

        gizmo.SetHandleLocked(anchor, false);
        bool snapRequested = false;
        gizmo.PositionConstraint = (_, proposed, snapping) =>
        {
            snapRequested = snapping;
            return new Vector3(Mathf.Round(proposed.X), Mathf.Round(proposed.Y), proposed.Z);
        };
        Vector3 target = start + new Vector3(0.6f, 1.4f, 0.0f);
        gizmo.BeginInteraction(viewport.Camera, viewport.Camera.UnprojectPosition(start));
        gizmo.UpdateInteraction(
            viewport.Camera,
            viewport.Camera.UnprojectPosition(target),
            snapping: true
        );
        gizmo.EndInteraction();

        AssertBool(snapRequested).IsTrue();
        AssertBool(
                curve.GetPointPosition(1).IsEqualApprox(new Vector3(1.0f, 1.0f, 0.0f))
            )
            .IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void SnapshotsPreserveCurvePropertiesPointsHandlesAndTilt()
    {
        using var source = CreateCurve();
        source.BakeInterval = 0.35f;
        source.Closed = true;
        source.UpVectorEnabled = false;
        source.SetPointTilt(1, 0.7f);
        BezierCurveSnapshot snapshot = BezierCurveSnapshot.Capture(source);
        using var restored = new Curve3D();

        snapshot.ApplyTo(restored);

        AssertCurveEquals(snapshot, restored);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task AddAndRemovePointUseWorldCoordinatesAndPublishEvents()
    {
        using var curve = CreateCurve();
        using var gizmo = new BezierCurveGizmo();
        MainViewport viewport = CreateViewport(new Vector3(0.0f, 0.0f, 10.0f));
        gizmo.RegisterViewport(viewport.Camera, viewport.ToolOverlayRoot, 1u << 20);
        var transform = new Transform3D(Basis.Identity, new Vector3(5.0f, 2.0f, 0.0f));
        gizmo.Show(curve, transform);
        await ProcessFrames(1);
        int added = -1;
        int removed = -1;
        gizmo.PointAdded += index => added = index;
        gizmo.PointRemoved += index => removed = index;

        int index = gizmo.AddPoint(new Vector3(6.0f, 4.0f, 0.0f), 1);

        AssertThat(index).IsEqual(1);
        AssertThat(added).IsEqual(1);
        AssertBool(
                curve.GetPointPosition(1).IsEqualApprox(new Vector3(1.0f, 2.0f, 0.0f))
            )
            .IsTrue();
        AssertThat(gizmo.SelectedHandle)
            .IsEqual(new BezierCurveHandle(1, BezierCurveHandleKind.Anchor));
        AssertBool(gizmo.RemovePoint(1)).IsTrue();
        AssertThat(removed).IsEqual(1);
        AssertThat(curve.PointCount).IsEqual(3);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task CollapsedCurve3DTangentsRemainVisibleAndDraggable()
    {
        using var curve = new Curve3D();
        curve.AddPoint(new Vector3(-3.0f, 0.0f, 0.0f));
        curve.AddPoint(new Vector3(3.0f, 0.0f, 0.0f));
        using var gizmo = new BezierCurveGizmo();
        MainViewport viewport = CreateViewport(new Vector3(0.0f, 0.0f, 10.0f));
        gizmo.RegisterViewport(viewport.Camera, viewport.ToolOverlayRoot, 1u << 20);
        gizmo.Show(curve, Transform3D.Identity);
        await ProcessFrames(1);
        Vector3 displayedOutgoing = new Vector3(-1.0f, 0.0f, 0.0f);
        Vector3 target = new Vector3(-1.0f, 1.0f, 0.0f);

        AssertThat(
                viewport.ToolOverlayRoot
                    .GetNode<MultiMeshInstance3D>("BezierCurveGizmo/Tangents")
                    .Multimesh.InstanceCount
            )
            .IsEqual(2);
        AssertBool(
                gizmo.BeginInteraction(
                    viewport.Camera,
                    viewport.Camera.UnprojectPosition(displayedOutgoing)
                )
            )
            .IsTrue();
        gizmo.UpdateInteraction(viewport.Camera, viewport.Camera.UnprojectPosition(target));
        gizmo.EndInteraction();

        AssertThat(gizmo.SelectedHandle)
            .IsEqual(new BezierCurveHandle(0, BezierCurveHandleKind.Outgoing));
        AssertBool(curve.GetPointOut(0).IsEqualApprox(new Vector3(2.0f, 1.0f, 0.0f)))
            .IsTrue();
    }

    private static Curve3D CreateCurve()
    {
        var curve = new Curve3D();
        curve.AddPoint(
            new Vector3(-3.0f, 0.0f, 0.0f),
            Vector3.Zero,
            new Vector3(1.0f, 0.0f, 0.0f)
        );
        curve.AddPoint(
            Vector3.Zero,
            new Vector3(-1.0f, 0.0f, 0.0f),
            new Vector3(1.0f, 0.0f, 0.0f)
        );
        curve.AddPoint(
            new Vector3(3.0f, 0.0f, 0.0f),
            new Vector3(-1.0f, 0.0f, 0.0f),
            Vector3.Zero
        );
        return curve;
    }

    private static void AssertCurveEquals(BezierCurveSnapshot expected, Curve3D actual)
    {
        AssertThat(actual.PointCount).IsEqual(expected.PointCount);
        AssertFloat(actual.BakeInterval).IsEqualApprox(expected.BakeInterval, 0.001f);
        AssertBool(actual.Closed).IsEqual(expected.Closed);
        AssertBool(actual.UpVectorEnabled).IsEqual(expected.UpVectorEnabled);
        for (int index = 0; index < expected.PointCount; index++)
        {
            AssertBool(actual.GetPointPosition(index).IsEqualApprox(expected[index].Position))
                .IsTrue();
            AssertBool(actual.GetPointIn(index).IsEqualApprox(expected[index].Incoming))
                .IsTrue();
            AssertBool(actual.GetPointOut(index).IsEqualApprox(expected[index].Outgoing))
                .IsTrue();
            AssertFloat(actual.GetPointTilt(index)).IsEqualApprox(expected[index].Tilt, 0.001f);
        }
    }

    private static MainViewport CreateViewport(Vector3 cameraPosition)
    {
        MainViewport viewport = AutoFree(
            new MainViewport { Position = Vector2.Zero, Size = ViewportSize }
        );
        AddToTree(viewport);
        viewport.Camera.LookAtFromPosition(cameraPosition, Vector3.Zero, Vector3.Up);
        return viewport;
    }

    private static void AddToTree(Node node) =>
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(node);

    private static async Task ProcessFrames(int count)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        for (int index = 0; index < count; index++)
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }
}
