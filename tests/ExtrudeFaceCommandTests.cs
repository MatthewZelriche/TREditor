using Godot;

namespace TREditor2026.Tests;

public sealed class ExtrudeFaceCommandTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );
    private static readonly SelectionTarget Face = SelectionTarget.ForFace(
        ObjectId,
        new FaceHandle(1, 0)
    );

    [Fact]
    public void CanCreate_RequiresExactlyOneSelectedFace()
    {
        Assert.True(ExtrudeFaceCommand.CanCreate(SelectionSnapshot.From([Face])));
        Assert.False(ExtrudeFaceCommand.CanCreate(SelectionSnapshot.Empty));
        Assert.False(
            ExtrudeFaceCommand.CanCreate(
                SelectionSnapshot.From([
                    Face,
                    SelectionTarget.ForVertex(ObjectId, new VertexHandle(2, 0)),
                ])
            )
        );
        Assert.False(
            ExtrudeFaceCommand.CanCreate(
                SelectionSnapshot.From([
                    Face,
                    SelectionTarget.ForFace(ObjectId, new FaceHandle(3, 0)),
                ])
            )
        );
    }

    [Fact]
    public void CreateIfChanged_ZeroDeltaReturnsNull()
    {
        Assert.Null(
            ExtrudeFaceCommand.CreateIfChanged(SelectionSnapshot.From([Face]), Vector3.Zero)
        );
    }

    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, true, false)]
    public void ShouldExtrudeFace_RequiresEditModeAndModifierOrSelectedOperation(
        bool editMode,
        bool shiftPressed,
        bool operationSelected,
        bool expected
    )
    {
        Assert.Equal(
            expected,
            SelectionTranslationGizmoController.ShouldExtrudeFace(
                SelectionSnapshot.From([Face]),
                editMode,
                shiftPressed,
                operationSelected
            )
        );
    }

    [Fact]
    public void ConstrainExtrusionDelta_LocalModeProjectsOntoFaceNormal()
    {
        Vector3 delta = new(3, 4, 5);

        Assert.Equal(
            new Vector3(0, 4, 0),
            SelectionTranslationGizmoController.ConstrainExtrusionDelta(delta, Vector3.Up)
        );
        Assert.Equal(
            delta,
            SelectionTranslationGizmoController.ConstrainExtrusionDelta(delta, Vector3.Zero)
        );
    }

    [Fact]
    public void CreateDragPreview_ExtrusionDragUsesTopologyPreview()
    {
        SelectionSnapshot selection = SelectionSnapshot.From([Face]);
        Vector3 delta = new(1, 2, 3);

        EditorPreviewRequest.ExtrudeFace preview = Assert.IsType<EditorPreviewRequest.ExtrudeFace>(
            SelectionTranslationGizmoController.CreateDragPreview(selection, delta, true)
        );

        Assert.Equal(Face, preview.Face);
        Assert.Equal(delta, preview.Delta);
    }

    [Fact]
    public void CreateDragPreview_OrdinaryDragUsesTranslationPreview()
    {
        SelectionSnapshot selection = SelectionSnapshot.From([Face]);

        Assert.IsType<EditorPreviewRequest.TranslateSelection>(
            SelectionTranslationGizmoController.CreateDragPreview(selection, Vector3.One, false)
        );
    }
}
