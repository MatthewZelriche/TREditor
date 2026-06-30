using Godot;

namespace TREditor2026.Tests;

public sealed class ExtrudeEdgeCommandTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );
    private static readonly SelectionTarget Edge = SelectionTarget.ForEdge(
        ObjectId,
        new HalfEdgeHandle(1, 0)
    );

    [Fact]
    public void CanCreate_RequiresExactlyOneSelectedEdge()
    {
        Assert.True(ExtrudeEdgeCommand.CanCreate(SelectionSnapshot.From([Edge])));
        Assert.False(ExtrudeEdgeCommand.CanCreate(SelectionSnapshot.Empty));
        Assert.False(
            ExtrudeEdgeCommand.CanCreate(
                SelectionSnapshot.From(
                    [Edge, SelectionTarget.ForEdge(ObjectId, new HalfEdgeHandle(2, 0))]
                )
            )
        );
        Assert.False(
            ExtrudeEdgeCommand.CanCreate(
                SelectionSnapshot.From([SelectionTarget.ForFace(ObjectId, new FaceHandle(1, 0))])
            )
        );
    }

    [Fact]
    public void CreateIfChanged_ZeroDeltaReturnsNull()
    {
        Assert.Null(
            ExtrudeEdgeCommand.CreateIfChanged(SelectionSnapshot.From([Edge]), Vector3.Zero)
        );
    }

    [Theory]
    [InlineData(true, true, false, true, true)]
    [InlineData(true, false, true, true, true)]
    [InlineData(true, false, false, true, false)]
    [InlineData(false, true, true, true, false)]
    [InlineData(true, true, true, false, false)]
    public void ShouldExtrudeEdge_RequiresEditModeInputAndOpenEdge(
        bool editMode,
        bool shiftPressed,
        bool operationSelected,
        bool canExtrude,
        bool expected
    )
    {
        Assert.Equal(
            expected,
            SelectionTranslationGizmoController.ShouldExtrudeEdge(
                SelectionSnapshot.From([Edge]),
                editMode,
                shiftPressed,
                operationSelected,
                canExtrude
            )
        );
    }

    [Fact]
    public void CreateDragPreview_EdgeExtrusionUsesTopologyPreview()
    {
        SelectionSnapshot selection = SelectionSnapshot.From([Edge]);
        Vector3 delta = new(1, 2, 3);

        EditorPreviewRequest.ExtrudeEdge preview = Assert.IsType<EditorPreviewRequest.ExtrudeEdge>(
            SelectionTranslationGizmoController.CreateDragPreview(
                selection,
                delta,
                extrudeFace: false,
                extrudeEdge: true
            )
        );

        Assert.Equal(Edge, preview.Edge);
        Assert.Equal(delta, preview.Delta);
    }
}
