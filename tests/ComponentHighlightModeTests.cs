namespace TREditor2026.Tests;

public sealed class ComponentHighlightModeTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );

    [Fact]
    public void EditMode_ShowsPassiveVerticesAndEdgesAndAllowsEveryComponent()
    {
        ComponentHighlightMode mode = ComponentHighlightMode.Edit;

        Assert.True(mode.PassiveKinds.Includes(ScenePickElementKind.Vertex));
        Assert.True(mode.PassiveKinds.Includes(ScenePickElementKind.Edge));
        Assert.False(mode.PassiveKinds.Includes(ScenePickElementKind.Face));
        Assert.True(mode.AllowsSelected(SelectionTarget.ForVertex(ObjectId, default)));
        Assert.True(mode.AllowsSelected(SelectionTarget.ForEdge(ObjectId, default)));
        Assert.True(mode.AllowsSelected(SelectionTarget.ForFace(ObjectId, default)));
        Assert.True(mode.AllowsHover(SelectionTarget.ForVertex(ObjectId, default)));
        Assert.True(mode.AllowsHover(SelectionTarget.ForEdge(ObjectId, default)));
        Assert.True(mode.AllowsHover(SelectionTarget.ForFace(ObjectId, default)));
    }

    [Fact]
    public void FaceHoverOnlyMode_HidesPassiveAndSelectedComponentsAndAllowsOnlyFaceHover()
    {
        ComponentHighlightMode mode = ComponentHighlightMode.FaceHoverOnly;

        Assert.Equal(ComponentHighlightKinds.None, mode.PassiveKinds);
        Assert.False(mode.AllowsSelected(SelectionTarget.ForFace(ObjectId, default)));
        Assert.False(mode.AllowsHover(SelectionTarget.ForVertex(ObjectId, default)));
        Assert.False(mode.AllowsHover(SelectionTarget.ForEdge(ObjectId, default)));
        Assert.True(mode.AllowsHover(SelectionTarget.ForFace(ObjectId, default)));
    }

    [Fact]
    public void EditComponents_CreatesReusableSingleComponentPolicy()
    {
        ComponentHighlightMode mode = ComponentHighlightMode.EditComponents(
            ComponentHighlightKinds.Edges
        );

        Assert.Equal(ComponentHighlightKinds.Edges, mode.PassiveKinds);
        Assert.True(mode.AllowsSelected(SelectionTarget.ForEdge(ObjectId, default)));
        Assert.True(mode.AllowsHover(SelectionTarget.ForEdge(ObjectId, default)));
        Assert.False(mode.AllowsSelected(SelectionTarget.ForVertex(ObjectId, default)));
        Assert.False(mode.AllowsHover(SelectionTarget.ForFace(ObjectId, default)));
    }

    [Fact]
    public void EditComponents_FaceSelectionDoesNotFillEveryPassiveFace()
    {
        ComponentHighlightMode mode = ComponentHighlightMode.EditComponents(
            ComponentHighlightKinds.Faces
        );

        Assert.Equal(ComponentHighlightKinds.None, mode.PassiveKinds);
        Assert.True(mode.AllowsSelected(SelectionTarget.ForFace(ObjectId, default)));
        Assert.True(mode.AllowsHover(SelectionTarget.ForFace(ObjectId, default)));
    }

    [Theory]
    [InlineData(ScenePickElementKind.None)]
    [InlineData(ScenePickElementKind.Object)]
    public void Includes_RejectsNonComponentKinds(ScenePickElementKind kind)
    {
        Assert.False(ComponentHighlightKinds.All.Includes(kind));
    }
}
