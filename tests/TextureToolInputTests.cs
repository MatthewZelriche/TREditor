using Godot;

namespace TREditor2026.Tests;

public sealed class TextureToolInputTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")
    );
    private static readonly FaceHandle Face = new(7, 2);
    private static readonly ViewportInputModifiers NoModifiers = new(false, false, false, false);

    [Fact]
    public void TryResolveApplyIntent_LeftPressOnFaceWithActiveTextureSucceeds()
    {
        ViewportMouseButtonEvent input = CreateInput(MouseButton.Left, pressed: true);
        ScenePickHit hit = ScenePickHit.FaceHit(ObjectId, Face, Vector3.Zero, 1);

        bool success = TextureToolInput.TryResolveApplyIntent(
            input,
            "walls/brick.png",
            hit,
            out TextureFaceApplyIntent intent
        );

        Assert.True(success);
        Assert.Equal("walls/brick.png", intent.AssetId);
        Assert.Equal(Face, intent.Face);
    }

    [Fact]
    public void TryResolveApplyIntent_SelectionModifiersDoNotChangePaintingIntent()
    {
        ViewportMouseButtonEvent input = CreateInput(
            MouseButton.Left,
            pressed: true,
            new ViewportInputModifiers(true, true, false, false)
        );

        Assert.True(
            TextureToolInput.TryResolveApplyIntent(
                input,
                "walls/brick.png",
                ScenePickHit.FaceHit(ObjectId, Face, Vector3.Zero, 1),
                out _
            )
        );
    }

    [Theory]
    [InlineData(MouseButton.Right, true)]
    [InlineData(MouseButton.Left, false)]
    public void TryResolveApplyIntent_NonLeftPressIsIgnored(MouseButton button, bool pressed)
    {
        Assert.False(
            TextureToolInput.TryResolveApplyIntent(
                CreateInput(button, pressed),
                "walls/brick.png",
                ScenePickHit.FaceHit(ObjectId, Face, Vector3.Zero, 1),
                out _
            )
        );
    }

    [Fact]
    public void TryResolveApplyIntent_ClickWithoutActiveTextureIsIgnored()
    {
        Assert.False(
            TextureToolInput.TryResolveApplyIntent(
                CreateInput(MouseButton.Left, pressed: true),
                null,
                ScenePickHit.FaceHit(ObjectId, Face, Vector3.Zero, 1),
                out _
            )
        );
    }

    [Theory]
    [InlineData(ScenePickElementKind.None)]
    [InlineData(ScenePickElementKind.Object)]
    [InlineData(ScenePickElementKind.Vertex)]
    [InlineData(ScenePickElementKind.Edge)]
    public void TryResolveApplyIntent_MissOrNonFaceHitIsIgnored(ScenePickElementKind kind)
    {
        ScenePickHit hit = kind switch
        {
            ScenePickElementKind.Object => ScenePickHit.ObjectHit(ObjectId, Vector3.Zero, 1),
            ScenePickElementKind.Vertex => ScenePickHit.VertexHit(
                ObjectId,
                default,
                Vector3.Zero,
                1
            ),
            ScenePickElementKind.Edge => ScenePickHit.EdgeHit(ObjectId, default, Vector3.Zero, 1),
            _ => ScenePickHit.None,
        };

        Assert.False(
            TextureToolInput.TryResolveApplyIntent(
                CreateInput(MouseButton.Left, pressed: true),
                "walls/brick.png",
                hit,
                out _
            )
        );
    }

    private static ViewportMouseButtonEvent CreateInput(
        MouseButton button,
        bool pressed,
        ViewportInputModifiers? modifiers = null
    ) =>
        new(
            "",
            Vector2.Zero,
            Vector2.Zero,
            button,
            pressed,
            false,
            modifiers ?? NoModifiers,
            Vector3.Zero,
            Vector3.Forward
        );
}
