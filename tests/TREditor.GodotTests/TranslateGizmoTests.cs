using System.Threading.Tasks;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace TREditor.GodotTests;

[TestSuite]
public class TranslateGizmoTests
{
    private static readonly Vector2 ViewportSize = new(640.0f, 480.0f);

    [TestCase]
    [RequireGodotRuntime]
    public async Task RegisteredViewportsGetIsolatedGizmoLayers()
    {
        Node3D proxyParent = AutoFree(new Node3D());
        AddToTree(proxyParent);
        var selection = new SelectionState();
        using var previews = new PreviewState();
        using var coordinator = new TranslateGizmoCoordinator(proxyParent, selection, previews);
        MainViewport first = CreateViewport(new Vector3(8.0f, 6.0f, 8.0f));
        MainViewport second = CreateViewport(new Vector3(-9.0f, 4.0f, 7.0f));

        uint firstDefaultCullMask = first.Camera.CullMask;
        coordinator.RegisterViewport(first.Camera, first.ToolOverlayRoot);
        coordinator.RegisterViewport(second.Camera, second.ToolOverlayRoot);
        await ProcessFrames(2);

        AssertThat(coordinator.ViewportCount).IsEqual(2);
        uint firstGizmoLayers = first.Camera.CullMask & GizmoLayerPool.ReservedMask;
        uint secondGizmoLayers = second.Camera.CullMask & GizmoLayerPool.ReservedMask;
        uint visibleSceneLayers = (1u << (GizmoLayerPool.FirstLayer - 1)) - 1u;
        AssertThat(firstGizmoLayers).IsNotEqual(0u);
        AssertThat(secondGizmoLayers).IsNotEqual(0u);
        // Neither camera may see the other pane's gizmo, and both keep their ordinary layers.
        AssertThat(firstGizmoLayers & secondGizmoLayers).IsEqual(0u);
        AssertThat(GizmoLayerPool.ReservedMask & visibleSceneLayers).IsEqual(0u);
        AssertThat(first.Camera.CullMask & visibleSceneLayers)
            .IsEqual(firstDefaultCullMask & visibleSceneLayers);
        AssertThat(first.Camera.CullMask & ~GizmoLayerPool.ReservedMask)
            .IsEqual(firstDefaultCullMask & ~GizmoLayerPool.ReservedMask);

        coordinator.UnregisterViewport(second.Camera);
        AssertThat(coordinator.ViewportCount).IsEqual(1);
        AssertThat(second.Camera.CullMask).IsEqual(firstDefaultCullMask);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void TheLayerPoolIsBoundedAndReclaimed()
    {
        var pool = new GizmoLayerPool();
        var allocated = new uint[GizmoLayerPool.LayerCount];
        uint combinedMask = 0u;
        for (int index = 0; index < allocated.Length; index++)
        {
            allocated[index] = pool.Allocate();
            combinedMask |= allocated[index];
        }

        AssertThat(pool.AvailableCount).IsEqual(0);
        AssertThat(combinedMask).IsEqual(GizmoLayerPool.ReservedMask);
        AssertThrown(() => pool.Allocate()).IsInstanceOf<System.InvalidOperationException>();

        pool.Release(allocated[2]);
        AssertThat(pool.AvailableCount).IsEqual(1);
        AssertThat(pool.Allocate()).IsEqual(allocated[2]);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task GizmosAppearForTheSharedProxyAndHideWithoutASelection()
    {
        Node3D proxyParent = AutoFree(new Node3D());
        AddToTree(proxyParent);
        var selection = new SelectionState();
        using var previews = new PreviewState();
        using var coordinator = new TranslateGizmoCoordinator(proxyParent, selection, previews);
        MainViewport first = CreateViewport(new Vector3(8.0f, 6.0f, 8.0f));
        MainViewport second = CreateViewport(new Vector3(-9.0f, 4.0f, 7.0f));
        coordinator.RegisterViewport(first.Camera, first.ToolOverlayRoot);
        coordinator.RegisterViewport(second.Camera, second.ToolOverlayRoot);

        AssertBool(coordinator.IsVisible).IsFalse();

        var target = new Transform3D(Basis.Identity, new Vector3(1.0f, 2.0f, -1.0f));
        coordinator.ShowAt(target);
        await ProcessFrames(2);

        AssertBool(coordinator.IsVisible).IsTrue();
        AssertThat(coordinator.ProxyTransform).IsEqual(target);
        // Both hosts follow the one proxy rather than holding separate targets.
        AssertBool(GizmoFor(first).Visible).IsTrue();
        AssertBool(GizmoFor(second).Visible).IsTrue();

        coordinator.Hide();
        await ProcessFrames(2);

        AssertBool(coordinator.IsVisible).IsFalse();
        AssertBool(GizmoFor(first).Visible).IsFalse();
        AssertBool(GizmoFor(second).Visible).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task GizmoHandlesKeepTheSameScreenSizeFromDifferentDistances()
    {
        Node3D proxyParent = AutoFree(new Node3D());
        AddToTree(proxyParent);
        var selection = new SelectionState();
        using var previews = new PreviewState();
        using var coordinator = new TranslateGizmoCoordinator(proxyParent, selection, previews);
        MainViewport near = CreateViewport(new Vector3(4.0f, 3.0f, 4.0f));
        MainViewport far = CreateViewport(new Vector3(24.0f, 18.0f, 24.0f));
        coordinator.RegisterViewport(near.Camera, near.ToolOverlayRoot);
        coordinator.RegisterViewport(far.Camera, far.ToolOverlayRoot);
        coordinator.ShowAt(Transform3D.Identity);
        await ProcessFrames(3);

        float nearReach = FarthestHitOffset(coordinator, near.Camera);
        float farReach = FarthestHitOffset(coordinator, far.Camera);

        AssertThat(nearReach).IsGreater(0.0f);
        // Constant-pixel sizing means the handle occupies the same screen distance regardless of how
        // far the camera is from the gizmo.
        AssertFloat(farReach).IsEqualApprox(nearReach, 12.0f);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task OnlyOneHostOwnsAnInteractionAndCancellationReleasesIt()
    {
        Node3D proxyParent = AutoFree(new Node3D());
        AddToTree(proxyParent);
        var selection = new SelectionState();
        using var previews = new PreviewState();
        using var coordinator = new TranslateGizmoCoordinator(proxyParent, selection, previews);
        MainViewport first = CreateViewport(new Vector3(6.0f, 5.0f, 6.0f));
        MainViewport second = CreateViewport(new Vector3(-7.0f, 5.0f, 6.0f));
        coordinator.RegisterViewport(first.Camera, first.ToolOverlayRoot);
        coordinator.RegisterViewport(second.Camera, second.ToolOverlayRoot);
        coordinator.ShowAt(Transform3D.Identity);
        await ProcessFrames(3);

        Vector2 handle = FindHandlePosition(coordinator, first.Camera);
        AssertBool(coordinator.BeginInteraction(first.Camera, handle)).IsTrue();
        AssertThat(coordinator.InteractionCamera).IsSame(first.Camera);

        // Input from a pane that does not own the drag is ignored.
        AssertBool(coordinator.UpdateInteraction(second.Camera, handle + new Vector2(40.0f, 0.0f)))
            .IsFalse();
        AssertBool(GizmoFor(second).IsInteracting).IsFalse();

        coordinator.Cancel();
        AssertBool(coordinator.IsInteracting).IsFalse();
        AssertBool(GizmoFor(first).IsInteracting).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task PressingAwayFromAHandleReportsNoGizmoHit()
    {
        Node3D proxyParent = AutoFree(new Node3D());
        AddToTree(proxyParent);
        var selection = new SelectionState();
        using var previews = new PreviewState();
        using var coordinator = new TranslateGizmoCoordinator(proxyParent, selection, previews);
        MainViewport viewport = CreateViewport(new Vector3(6.0f, 5.0f, 6.0f));
        coordinator.RegisterViewport(viewport.Camera, viewport.ToolOverlayRoot);
        coordinator.ShowAt(Transform3D.Identity);
        await ProcessFrames(3);

        Vector2 center = viewport.Camera.UnprojectPosition(Vector3.Zero);

        AssertBool(coordinator.BeginInteraction(viewport.Camera, center + new Vector2(0.0f, 260.0f)))
            .IsFalse();
        AssertBool(coordinator.IsInteracting).IsFalse();

        // A hidden gizmo cannot be hit even where a handle would otherwise be.
        Vector2 handle = FindHandlePosition(coordinator, viewport.Camera);
        coordinator.Hide();
        await ProcessFrames(2);
        AssertBool(coordinator.BeginInteraction(viewport.Camera, handle)).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task DraggingMovesOnlyTheProxy()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        AddToTree(mapRoot);
        var selection = new SelectionState();
        using var previews = new PreviewState();
        using var coordinator = new TranslateGizmoCoordinator(mapRoot, selection, previews);
        MainViewport viewport = CreateViewport(new Vector3(6.0f, 5.0f, 6.0f));
        coordinator.RegisterViewport(viewport.Camera, viewport.ToolOverlayRoot);
        coordinator.ShowAt(Transform3D.Identity);
        await ProcessFrames(3);

        Vector2 handle = FindHandlePosition(coordinator, viewport.Camera);
        AssertBool(coordinator.BeginInteraction(viewport.Camera, handle)).IsTrue();
        AssertBool(coordinator.UpdateInteraction(viewport.Camera, handle + new Vector2(60.0f, 0.0f)))
            .IsTrue();
        await ProcessFrames(1);

        Transform3D dragged = coordinator.ProxyTransform;
        AssertBool(dragged.Origin.IsEqualApprox(Vector3.Zero)).IsFalse();

        coordinator.EndInteraction();
        AssertBool(coordinator.IsInteracting).IsFalse();
        // Ending the interaction commits nothing on its own; the proxy simply keeps its position.
        AssertThat(coordinator.ProxyTransform).IsEqual(dragged);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task ApplicationShowsTheGizmoOnTheSelectedObject()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        AddToTree(mapRoot);
        using var application = new Application(mapRoot);
        MainViewport viewport = CreateViewport(new Vector3(9.0f, 7.0f, 9.0f));
        application.RegisterToolViewport(viewport.Camera, viewport.ToolOverlayRoot);
        await ProcessFrames(2);

        AssertBool(application.Gizmos.IsVisible).IsFalse();

        MeshObjectId id = FirstObjectId(application);
        application.ToolContext.Selection.Select(id);
        AssertBool(application.Document!.TryGetMeshTransform(id, out Transform3D committed)).IsTrue();

        AssertBool(application.Gizmos.IsVisible).IsTrue();
        AssertThat(application.Gizmos.ProxyTransform.Origin).IsEqual(committed.Origin);

        // Committed scene notifications reach the coordinator through SceneServices.
        var updatedCommitted = new Transform3D(
            committed.Basis,
            committed.Origin + new Vector3(0.0f, 2.0f, 0.0f)
        );
        AssertBool(application.CurrentScene!.SetTransform(id, updatedCommitted)).IsTrue();
        AssertThat(application.Gizmos.ProxyTransform.Origin).IsEqual(updatedCommitted.Origin);

        // A preview moves the gizmo without the document changing.
        var previewed = new Transform3D(
            updatedCommitted.Basis,
            updatedCommitted.Origin + new Vector3(3.0f, 0.0f, 0.0f)
        );
        application.ToolContext.Preview.SetTransform(id, previewed);
        AssertThat(application.Gizmos.ProxyTransform.Origin).IsEqual(previewed.Origin);
        AssertBool(application.Document.TryGetMeshTransform(id, out Transform3D unchanged)).IsTrue();
        AssertThat(unchanged.Origin).IsEqual(updatedCommitted.Origin);

        application.ToolContext.Preview.ClearTransform(id);
        AssertThat(application.Gizmos.ProxyTransform.Origin).IsEqual(updatedCommitted.Origin);

        application.ToolContext.Selection.Clear();
        AssertBool(application.Gizmos.IsVisible).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task ReplacingTheSceneHidesTheGizmo()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        AddToTree(mapRoot);
        using var application = new Application(mapRoot);
        MainViewport viewport = CreateViewport(new Vector3(9.0f, 7.0f, 9.0f));
        application.RegisterToolViewport(viewport.Camera, viewport.ToolOverlayRoot);
        application.ToolContext.Selection.Select(FirstObjectId(application));
        await ProcessFrames(2);
        AssertBool(application.Gizmos.IsVisible).IsTrue();

        application.NewScene();

        AssertBool(application.Gizmos.IsVisible).IsFalse();
        AssertThat(application.ToolContext.Selection.Selected).IsNull();
    }

    private static MeshObjectId FirstObjectId(Application application)
    {
        foreach (MeshObjectId id in application.Document!.GetObjectIds())
            return id;

        AssertThat("the debug scene has objects").IsEqual("no objects were found");
        return default;
    }

    // Walks outward along the projected +X axis and reports the farthest offset that still grabs a
    // handle, which is a screen-space measure of the gizmo's size.
    private static float FarthestHitOffset(TranslateGizmoCoordinator coordinator, Camera3D camera)
    {
        float farthest = 0.0f;
        foreach (float offset in Offsets())
        {
            if (TryHit(coordinator, camera, offset))
                farthest = offset;
        }

        return farthest;
    }

    private static Vector2 FindHandlePosition(
        TranslateGizmoCoordinator coordinator,
        Camera3D camera
    )
    {
        foreach (float offset in Offsets())
        {
            if (TryHit(coordinator, camera, offset))
                return HandlePosition(camera, offset);
        }

        AssertThat("a handle was found").IsEqual("no handle was hit");
        return Vector2.Zero;
    }

    private static bool TryHit(
        TranslateGizmoCoordinator coordinator,
        Camera3D camera,
        float offset
    )
    {
        bool hit = coordinator.BeginInteraction(camera, HandlePosition(camera, offset));
        coordinator.Cancel();
        return hit;
    }

    private static Vector2 HandlePosition(Camera3D camera, float offset)
    {
        Vector2 center = camera.UnprojectPosition(Vector3.Zero);
        Vector2 axis = (camera.UnprojectPosition(Vector3.Right) - center).Normalized();
        return center + (axis * offset);
    }

    private static float[] Offsets()
    {
        var offsets = new float[60];
        for (int index = 0; index < offsets.Length; index++)
            offsets[index] = 4.0f * (index + 1);

        return offsets;
    }

    private static TranslateGizmoHost GizmoFor(MainViewport viewport)
    {
        foreach (Node child in viewport.ToolOverlayRoot.GetChildren())
        {
            if (child is TranslateGizmoHost host)
                return host;
        }

        AssertThat("a gizmo host exists").IsEqual("the overlay root has no host");
        return null!;
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
