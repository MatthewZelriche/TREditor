using Godot;

namespace TREditor2026.Tests;

public class ObjectPickResolverTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );

    [Fact]
    public void ResolveCandidate_UsesClosestGeometricHit()
    {
        Vector3 frontSurface = new(0, 0, 2);
        ScenePickHit hit = ObjectPickResolver.ResolveCandidate(
            ObjectId,
            ScenePickHit.VertexHit(ObjectId, default, new Vector3(0, 0, 4), 4.0f),
            ScenePickHit.EdgeHit(ObjectId, default, new Vector3(0, 0, 3), 3.0f),
            ScenePickHit.FaceHit(ObjectId, default, frontSurface, 2.0f)
        );

        Assert.Equal(ScenePickElementKind.Object, hit.Kind);
        Assert.Equal(ObjectId, hit.ObjectId);
        Assert.Equal(frontSurface, hit.Position);
        Assert.Equal(2.0f, hit.Distance);
    }

    [Fact]
    public void ResolveCandidate_PreservesFuzzyEdgeHitWhenRayMissesFaces()
    {
        Vector3 edgePosition = new(0, 0, 3);
        ScenePickHit hit = ObjectPickResolver.ResolveCandidate(
            ObjectId,
            ScenePickHit.None,
            ScenePickHit.EdgeHit(ObjectId, default, edgePosition, 3.0f),
            ScenePickHit.None
        );

        Assert.Equal(ScenePickElementKind.Object, hit.Kind);
        Assert.Equal(edgePosition, hit.Position);
        Assert.Equal(3.0f, hit.Distance);
    }

    [Fact]
    public void ResolveCandidate_NoComponentHits_ReturnsNone()
    {
        ScenePickHit hit = ObjectPickResolver.ResolveCandidate(
            ObjectId,
            ScenePickHit.None,
            ScenePickHit.None,
            ScenePickHit.None
        );

        Assert.False(hit.HasHit);
    }
}
