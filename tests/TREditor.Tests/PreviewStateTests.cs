using Godot;

namespace TREditor.Tests;

public sealed class PreviewStateTests
{
    private static readonly Transform3D FirstPreview = new(
        Basis.Identity,
        new Vector3(1.0f, 0.0f, 0.0f)
    );

    private static readonly Transform3D SecondPreview = new(
        Basis.Identity,
        new Vector3(2.0f, 0.0f, 0.0f)
    );

    [Fact]
    public void HasNoPreviewUntilOneIsSet()
    {
        var previews = new PreviewState();

        Assert.False(previews.TryGetTransform(new MeshObjectId(0), out Transform3D transform));
        Assert.Equal(default, transform);
    }

    [Fact]
    public void SetTransformStoresThePreviewAndAnnouncesTheObject()
    {
        var previews = new PreviewState();
        var id = new MeshObjectId(7);
        List<MeshObjectId> changed = Record(previews);

        previews.SetTransform(id, FirstPreview);

        Assert.True(previews.TryGetTransform(id, out Transform3D transform));
        Assert.Equal(FirstPreview, transform);
        Assert.Equal([id], changed);
    }

    [Fact]
    public void SetTransformAnnouncesEachDistinctValueDuringADrag()
    {
        var previews = new PreviewState();
        var id = new MeshObjectId(3);
        previews.SetTransform(id, FirstPreview);
        List<MeshObjectId> changed = Record(previews);

        previews.SetTransform(id, SecondPreview);

        Assert.True(previews.TryGetTransform(id, out Transform3D transform));
        Assert.Equal(SecondPreview, transform);
        Assert.Equal([id], changed);
    }

    [Fact]
    public void SetTransformIgnoresAnUnchangedValue()
    {
        var previews = new PreviewState();
        var id = new MeshObjectId(3);
        previews.SetTransform(id, FirstPreview);
        List<MeshObjectId> changed = Record(previews);

        previews.SetTransform(id, FirstPreview);

        Assert.Empty(changed);
    }

    [Fact]
    public void ClearTransformRemovesOnlyTheRequestedObject()
    {
        var previews = new PreviewState();
        var cleared = new MeshObjectId(1);
        var kept = new MeshObjectId(2);
        previews.SetTransform(cleared, FirstPreview);
        previews.SetTransform(kept, SecondPreview);
        List<MeshObjectId> changed = Record(previews);

        previews.ClearTransform(cleared);

        Assert.False(previews.TryGetTransform(cleared, out _));
        Assert.True(previews.TryGetTransform(kept, out _));
        Assert.Equal([cleared], changed);
    }

    [Fact]
    public void ClearTransformIgnoresAnObjectWithNoPreview()
    {
        var previews = new PreviewState();
        List<MeshObjectId> changed = Record(previews);

        previews.ClearTransform(new MeshObjectId(4));

        Assert.Empty(changed);
    }

    [Fact]
    public void ClearAllAnnouncesEveryPreviewedObjectOnce()
    {
        var previews = new PreviewState();
        var first = new MeshObjectId(1);
        var second = new MeshObjectId(2);
        previews.SetTransform(first, FirstPreview);
        previews.SetTransform(second, SecondPreview);
        List<MeshObjectId> changed = Record(previews);

        previews.ClearAll();

        Assert.False(previews.TryGetTransform(first, out _));
        Assert.False(previews.TryGetTransform(second, out _));
        Assert.Equal([first, second], changed.OrderBy(id => id.Value));

        previews.ClearAll();
        Assert.Equal(2, changed.Count);
    }

    [Fact]
    public void DocumentReplacementDiscardsPreviewsWithoutAnnouncingThem()
    {
        using var oldDocument = new Document();
        using var newDocument = new Document();
        var previews = new PreviewState();
        var id = new MeshObjectId(1);
        previews.SetDocument(oldDocument);
        previews.SetTransform(id, FirstPreview);
        List<MeshObjectId> changed = Record(previews);

        previews.SetDocument(newDocument);

        Assert.False(previews.TryGetTransform(id, out _));
        Assert.Empty(changed);
    }

    [Fact]
    public void ObjectRemovalDiscardsThatObjectsPreviewWithoutAnnouncingIt()
    {
        using var document = new Document();
        var previews = new PreviewState();
        MeshObject removed = document.Add("Removed", Transform3D.Identity, null!);
        MeshObject kept = document.Add("Kept", Transform3D.Identity, null!);
        previews.SetTransform(removed.Id, FirstPreview);
        previews.SetTransform(kept.Id, SecondPreview);
        List<MeshObjectId> changed = Record(previews);

        previews.OnDocumentChanged(
            new DocumentChangeSet(
                1,
                new DocumentObjectChange(removed.Id, DocumentChangeKind.Removed)
            )
        );

        Assert.False(previews.TryGetTransform(removed.Id, out _));
        Assert.True(previews.TryGetTransform(kept.Id, out _));
        Assert.Empty(changed);
    }

    [Fact]
    public void DisposeDropsPreviewsAndRejectsFurtherUse()
    {
        var previews = new PreviewState();
        var id = new MeshObjectId(1);
        previews.SetTransform(id, FirstPreview);
        List<MeshObjectId> changed = Record(previews);

        previews.Dispose();
        previews.Dispose();

        Assert.Empty(changed);
        Assert.Throws<ObjectDisposedException>(() => previews.TryGetTransform(id, out _));
        Assert.Throws<ObjectDisposedException>(() => previews.SetTransform(id, FirstPreview));
        Assert.Throws<ObjectDisposedException>(() => previews.ClearTransform(id));
        Assert.Throws<ObjectDisposedException>(() => previews.ClearAll());
    }

    private static List<MeshObjectId> Record(PreviewState previews)
    {
        var changed = new List<MeshObjectId>();
        previews.TransformChanged += changed.Add;
        return changed;
    }
}
