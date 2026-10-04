using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace TREditor.GodotTests;

[TestSuite]
public class GridRenderServiceTests
{
    [TestCase]
    [RequireGodotRuntime]
    public void EachViewportGetsAnIsolatedGridThatSurvivesDocumentReplacement()
    {
        Camera3D firstCamera = AutoFree(new Camera3D());
        Camera3D secondCamera = AutoFree(new Camera3D());
        Node3D firstOverlay = AutoFree(new Node3D());
        Node3D secondOverlay = AutoFree(new Node3D());
        using var firstDocument = new Document();
        using var secondDocument = new Document();
        var previews = new PreviewState();
        var gridRenderer = new GridRenderService();
        var services = new SceneServices(
            firstDocument,
            previews,
            new SelectionState(),
            new SelectionModeState(),
            gridRenderer
        );
        const uint firstLayer = 1u << 20;
        const uint secondLayer = 1u << 21;

        gridRenderer.RegisterViewport(firstCamera, firstOverlay, firstLayer);
        gridRenderer.RegisterViewport(secondCamera, secondOverlay, secondLayer);

        MeshInstance3D firstGrid = firstOverlay.GetNode<MeshInstance3D>("InfiniteGrid");
        MeshInstance3D secondGrid = secondOverlay.GetNode<MeshInstance3D>("InfiniteGrid");
        AssertThat(gridRenderer.ViewportCount).IsEqual(2);
        AssertThat(firstGrid).IsNotSame(secondGrid);
        AssertThat(firstGrid.Layers).IsEqual(firstLayer);
        AssertThat(secondGrid.Layers).IsEqual(secondLayer);
        AssertThat(firstGrid.Mesh).IsSame(secondGrid.Mesh);
        AssertThat(firstGrid.MaterialOverride).IsSame(secondGrid.MaterialOverride);
        AssertThat(firstGrid.Mesh).IsInstanceOf<QuadMesh>();
        AssertThat(((QuadMesh)firstGrid.Mesh).Size).IsEqual(new Vector2(2.0f, 2.0f));
        AssertBool(firstGrid.IgnoreOcclusionCulling).IsTrue();
        AssertBool(firstGrid.CustomAabb.HasPoint(new Vector3(1.0e9f, 0.0f, 1.0e9f))).IsTrue();
        AssertThat(firstGrid.CastShadow).IsEqual(GeometryInstance3D.ShadowCastingSetting.Off);
        AssertThat(firstGrid.MaterialOverride).IsInstanceOf<ShaderMaterial>();

        var material = (ShaderMaterial)firstGrid.MaterialOverride;
        AssertThat(material.Shader).IsNotNull();
        AssertThat(material.Shader.ResourcePath).IsEqual(GridRenderService.ShaderPath);
        AssertThat(material.RenderPriority).IsEqual(-100);
        AssertThat((float)material.GetShaderParameter("plane_height")).IsEqual(0.0f);
        AssertThat((float)material.GetShaderParameter("lod_level_count"))
            .IsEqual(GridRenderService.LodLevelCount);
        AssertThat(GridRenderService.LodLevelCount).IsGreater(2.0f);
        AssertThat((float)material.GetShaderParameter("minor_line_width"))
            .IsEqual(GridRenderService.MinorLineWidth);
        AssertThat((float)material.GetShaderParameter("major_line_width"))
            .IsEqual(GridRenderService.MajorLineWidth);
        AssertThat((float)material.GetShaderParameter("axis_line_width"))
            .IsEqual(GridRenderService.AxisLineWidth);

        services.SetDocument(secondDocument);

        AssertThat(firstOverlay.GetNode<MeshInstance3D>("InfiniteGrid")).IsSame(firstGrid);
        AssertThat(secondOverlay.GetNode<MeshInstance3D>("InfiniteGrid")).IsSame(secondGrid);

        gridRenderer.UnregisterViewport(firstCamera);
        AssertThat(gridRenderer.ViewportCount).IsEqual(1);
        AssertBool(firstOverlay.HasNode("InfiniteGrid")).IsFalse();

        services.Dispose();
        AssertBool(secondOverlay.HasNode("InfiniteGrid")).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ApplicationSharesEachViewportLayerWithOnlyItsOwnGrid()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        MainViewport first = AutoFree(new MainViewport());
        MainViewport second = AutoFree(new MainViewport());
        using var application = new Application(mapRoot);

        application.RegisterToolViewport(first.Camera, first.ToolOverlayRoot);
        application.RegisterToolViewport(second.Camera, second.ToolOverlayRoot);

        MeshInstance3D firstGrid = first.ToolOverlayRoot.GetNode<MeshInstance3D>("InfiniteGrid");
        MeshInstance3D secondGrid = second.ToolOverlayRoot.GetNode<MeshInstance3D>("InfiniteGrid");
        uint firstLayer = first.Camera.CullMask & GizmoLayerPool.ReservedMask;
        uint secondLayer = second.Camera.CullMask & GizmoLayerPool.ReservedMask;
        AssertThat(firstGrid.Layers).IsEqual(firstLayer);
        AssertThat(secondGrid.Layers).IsEqual(secondLayer);
        AssertThat(firstLayer & secondLayer).IsEqual(0u);

        application.UnregisterToolViewport(first.Camera);

        AssertBool(first.ToolOverlayRoot.HasNode("InfiniteGrid")).IsFalse();
        AssertBool(second.ToolOverlayRoot.HasNode("InfiniteGrid")).IsTrue();
    }
}
