namespace TREditor2026.Tests;

public sealed class EditOperationCatalogTests
{
    [Fact]
    public void Catalog_ContainsAvailableEditorOperations()
    {
        EditOperationDefinition extrude = EditOperationCatalog.Get("ExtrudeFace");
        EditOperationDefinition inset = EditOperationCatalog.Get("InsetFace");
        EditOperationDefinition fillHole = EditOperationCatalog.Get("FillHole");
        EditOperationDefinition collapseFace = EditOperationCatalog.Get("CollapseFace");
        EditOperationDefinition bevelEdge = EditOperationCatalog.Get("BevelEdge");
        EditOperationDefinition bevelVertex = EditOperationCatalog.Get("BevelVertex");
        EditOperationDefinition collapseVertices = EditOperationCatalog.Get("CollapseVertices");
        EditOperationDefinition bridgeEdges = EditOperationCatalog.Get("BridgeEdges");
        EditOperationDefinition detachFace = EditOperationCatalog.Get("DetachFace");
        EditOperationDefinition delete = EditOperationCatalog.Get("DeleteSelection");

        Assert.Equal(EditOperationAvailability.Available, extrude.Availability);
        Assert.Equal(EditOperationAvailability.Available, inset.Availability);
        Assert.Equal(EditOperationAvailability.Available, fillHole.Availability);
        Assert.Equal(EditOperationAvailability.Available, collapseFace.Availability);
        Assert.Equal(EditOperationAvailability.Available, bevelEdge.Availability);
        Assert.Equal(EditOperationAvailability.Available, bevelVertex.Availability);
        Assert.Equal(EditOperationAvailability.Available, collapseVertices.Availability);
        Assert.Equal(EditOperationAvailability.Available, bridgeEdges.Availability);
        Assert.Equal(EditOperationAvailability.Available, detachFace.Availability);
        Assert.Equal(EditOperationAvailability.Available, delete.Availability);
    }

    [Theory]
    [InlineData("EdgeCut")]
    public void Catalog_IdentifiesOperationsWithExistingCoreSupport(string id)
    {
        Assert.Equal(
            EditOperationAvailability.CoreReady,
            EditOperationCatalog.Get(id).Availability
        );
    }

    [Fact]
    public void Catalog_HasUniqueIds()
    {
        Assert.Equal(
            EditOperationCatalog.All.Count,
            EditOperationCatalog.All.Select(operation => operation.Id).Distinct().Count()
        );
    }

    [Fact]
    public void OperationTooltip_IncludesDescriptionSelectionAndInput()
    {
        EditOperationDefinition operation = EditOperationCatalog.Get("ExtrudeFace");

        string tooltip = EditPanel.BuildTooltip(operation);

        Assert.Contains(operation.Description, tooltip);
        Assert.Contains($"Selection: {operation.Selection}", tooltip);
        Assert.Contains($"Input: {operation.Input}", tooltip);
    }

    [Fact]
    public void EditOperationSettings_AllowsSelectionAndDeselection()
    {
        EditOperationSettings settings = new();

        settings.Select("ExtrudeFace");
        Assert.True(settings.IsSelected("ExtrudeFace"));

        settings.Deselect();
        Assert.Null(settings.SelectedOperationId);
    }

    [Fact]
    public void EditOperationSettings_LocalExtrusionDefaultsOnAndCanBeDisabled()
    {
        EditOperationSettings settings = new();

        Assert.True(settings.ExtrudeAlongFaceNormal);

        settings.SetExtrudeAlongFaceNormal(false);

        Assert.False(settings.ExtrudeAlongFaceNormal);
    }

    [Fact]
    public void EditOperationSettings_InsetDepthDefaultsAndCanBeChanged()
    {
        EditOperationSettings settings = new();

        Assert.Equal(0.25f, settings.InsetDepth);

        settings.SetInsetDepth(0.75f);

        Assert.Equal(0.75f, settings.InsetDepth);
    }

    [Fact]
    public void EditOperationSettings_BevelWidthDefaultsAndCanBeChanged()
    {
        EditOperationSettings settings = new();

        Assert.Equal(0.25f, settings.BevelWidth);

        settings.SetBevelWidth(0.75f);

        Assert.Equal(0.75f, settings.BevelWidth);
    }

    [Fact]
    public void EditOperationSettings_CollapseVerticesTargetDefaultsToFirstAndCanChange()
    {
        EditOperationSettings settings = new();

        Assert.Equal(CollapseVerticesTarget.First, settings.TwoVertexCollapseTarget);

        settings.SetTwoVertexCollapseTarget(CollapseVerticesTarget.Second);

        Assert.Equal(CollapseVerticesTarget.Second, settings.TwoVertexCollapseTarget);
    }

    [Fact]
    public void EditOperationSettings_BridgeDefaultsAndCanChange()
    {
        EditOperationSettings settings = new();

        Assert.Equal(4, settings.BridgeSegments);
        Assert.Equal(180f, settings.BridgeArchAngleDegrees);

        settings.SetBridgeSegments(8);
        settings.SetBridgeArchAngleDegrees(90f);

        Assert.Equal(8, settings.BridgeSegments);
        Assert.Equal(90f, settings.BridgeArchAngleDegrees);
    }

    [Theory]
    [InlineData(0.25f, "0.25")]
    [InlineData(0.005f, "0.0050")]
    public void FormatInsetDepth_PreservesUsefulPrecision(float depth, string expected)
    {
        Assert.Equal(expected, EditPanel.FormatInsetDepth(depth));
    }
}
