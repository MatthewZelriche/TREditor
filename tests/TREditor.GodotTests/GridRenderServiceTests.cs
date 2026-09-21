using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace TREditor.GodotTests;

[TestSuite]
public class GridRenderServiceTests
{
    [TestCase]
    [RequireGodotRuntime]
    public void GridIsAFullscreenTransparentShaderPlaneThatSurvivesDocumentReplacement()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var firstDocument = new Document();
        using var secondDocument = new Document();
        var previews = new PreviewState();
        var gridRenderer = new GridRenderService(mapRoot);
        var services = new SceneServices(firstDocument, previews, gridRenderer);

        MeshInstance3D grid = mapRoot.GetNode<MeshInstance3D>("InfiniteGrid");
        AssertThat(grid.Mesh).IsInstanceOf<QuadMesh>();
        AssertThat(((QuadMesh)grid.Mesh).Size).IsEqual(new Vector2(2.0f, 2.0f));
        AssertBool(grid.IgnoreOcclusionCulling).IsTrue();
        AssertBool(grid.CustomAabb.HasPoint(new Vector3(1.0e9f, 0.0f, 1.0e9f))).IsTrue();
        AssertThat(grid.CastShadow).IsEqual(GeometryInstance3D.ShadowCastingSetting.Off);
        AssertThat(grid.MaterialOverride).IsInstanceOf<ShaderMaterial>();

        var material = (ShaderMaterial)grid.MaterialOverride;
        AssertThat(material.Shader).IsNotNull();
        AssertThat(material.Shader.ResourcePath).IsEqual(GridRenderService.ShaderPath);
        AssertThat(material.RenderPriority).IsEqual(-100);
        AssertThat((float)material.GetShaderParameter("plane_height")).IsEqual(0.0f);

        services.SetDocument(secondDocument);

        AssertThat(mapRoot.GetNode<MeshInstance3D>("InfiniteGrid")).IsSame(grid);
        AssertThat(mapRoot.GetChildCount()).IsEqual(1);

        services.Dispose();
        AssertBool(mapRoot.HasNode("InfiniteGrid")).IsFalse();
    }
}
