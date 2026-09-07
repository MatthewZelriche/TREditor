using System.Reflection;
using Godot;

namespace TREditor.Tests;

public class DocumentTests
{
    [Fact]
    public void AddAllocatesMonotonicIdsWithoutReuse()
    {
        using var document = new Document();

        MeshObject first = document.Add("First", Transform3D.Identity, null!);
        MeshObject second = document.Add("Second", Transform3D.Identity, null!);
        MeshObject? removed = document.Remove(first.Id);
        Assert.NotNull(removed);
        removed.Mesh?.Dispose();
        MeshObject third = document.Add("Third", Transform3D.Identity, null!);

        Assert.Equal(new MeshObjectId(0), first.Id);
        Assert.Equal(new MeshObjectId(1), second.Id);
        Assert.Equal(new MeshObjectId(2), third.Id);
        Assert.False(document.TryGetObject(first.Id, out _));
        Assert.True(document.TryGetObject(second.Id, out MeshObject currentSecond));
        Assert.Same(second, currentSecond);
        Assert.True(document.TryGetObject(third.Id, out MeshObject currentThird));
        Assert.Same(third, currentThird);
    }

    [Fact]
    public void MutationsRaiseOneChangedEventWithTheExpectedPayload()
    {
        using var document = new Document();
        var changeSets = new List<DocumentChangeSet>();
        document.Changed += changeSets.Add;

        MeshObject meshObject = document.Add("Object", Transform3D.Identity, null!);
        var transform = new Transform3D(Basis.Identity, new Vector3(1, 2, 3));
        MeshObject replacement = meshObject with { Transform = transform };
        Assert.True(document.ReplaceObject(meshObject, replacement));
        Assert.True(document.CommitGeometryChange(meshObject.Id));
        MeshObject? removed = document.Remove(meshObject.Id);
        Assert.NotNull(removed);

        Assert.Equal(4, changeSets.Count);
        Assert.Equal(1, changeSets[0].MutationSerial);
        Assert.Equal(
            new DocumentObjectChange(meshObject.Id, DocumentChangeKind.Added),
            changeSets[0].Change
        );
        Assert.Equal(2, changeSets[1].MutationSerial);
        Assert.Equal(
            new DocumentObjectChange(meshObject.Id, DocumentChangeKind.TransformChanged),
            changeSets[1].Change
        );
        Assert.Equal(Transform3D.Identity, meshObject.Transform);
        Assert.Equal(3, changeSets[2].MutationSerial);
        Assert.Equal(
            new DocumentObjectChange(meshObject.Id, DocumentChangeKind.GeometryChanged),
            changeSets[2].Change
        );
        Assert.Equal(4, changeSets[3].MutationSerial);
        Assert.Equal(
            new DocumentObjectChange(meshObject.Id, DocumentChangeKind.Removed),
            changeSets[3].Change
        );
        Assert.Equal(meshObject.Id, removed.Id);
        Assert.Equal(transform, removed.Transform);
        Assert.NotSame(meshObject, removed);
        Assert.Equal(4, document.MutationSerial);
    }

    [Fact]
    public void ReplaceObjectDoesNotRaiseGeometryChanged()
    {
        using var document = new Document();
        MeshObject meshObject = document.Add("Object", Transform3D.Identity, null!);
        var kinds = new List<DocumentChangeKind>();
        document.Changed += changeSet => kinds.Add(changeSet.Change.Kind);
        kinds.Clear();

        Assert.True(
            document.ReplaceObject(
                meshObject,
                meshObject with { Transform = new Transform3D(Basis.Identity, new Vector3(4, 5, 6)) }
            )
        );

        Assert.Equal([DocumentChangeKind.TransformChanged], kinds);
    }

    [Fact]
    public void ReplaceObjectInstallsTheReplacementAndRaisesTransformChangedOnce()
    {
        using var document = new Document();
        MeshObject expected = document.Add("Object", Transform3D.Identity, null!);
        MeshObject replacement = expected with
        {
            Transform = new Transform3D(Basis.Identity, new Vector3(1, 2, 3)),
        };
        var changeSets = new List<DocumentChangeSet>();
        document.Changed += changeSets.Add;
        long serialBefore = document.MutationSerial;

        Assert.True(document.ReplaceObject(expected, replacement));

        Assert.Single(changeSets);
        Assert.Equal(serialBefore + 1, changeSets[0].MutationSerial);
        Assert.Equal(
            new DocumentObjectChange(expected.Id, DocumentChangeKind.TransformChanged),
            changeSets[0].Change
        );
        Assert.True(document.TryGetObject(expected.Id, out MeshObject current));
        Assert.Same(replacement, current);
    }

    [Fact]
    public void ReplaceObjectRejectsAStaleExpectedRecordWithoutMutationOrEvent()
    {
        using var document = new Document();
        MeshObject original = document.Add("Object", Transform3D.Identity, null!);
        MeshObject installed = original with
        {
            Transform = new Transform3D(Basis.Identity, new Vector3(1, 0, 0)),
        };
        Assert.True(document.ReplaceObject(original, installed));
        MeshObject staleAttempt = original with
        {
            Transform = new Transform3D(Basis.Identity, new Vector3(9, 9, 9)),
        };
        int changedCount = 0;
        document.Changed += _ => changedCount++;
        long serialBefore = document.MutationSerial;

        Assert.False(document.ReplaceObject(original, staleAttempt));

        Assert.Equal(0, changedCount);
        Assert.Equal(serialBefore, document.MutationSerial);
        Assert.True(document.TryGetObject(original.Id, out MeshObject current));
        Assert.Same(installed, current);
    }

    [Fact]
    public void ReplaceObjectRejectsAMissingTargetWithoutMutationOrEvent()
    {
        using var document = new Document();
        var orphan = new MeshObject(
            new MeshObjectId(42),
            "Missing",
            Transform3D.Identity,
            null!
        );
        int changedCount = 0;
        document.Changed += _ => changedCount++;
        long serialBefore = document.MutationSerial;

        Assert.False(
            document.ReplaceObject(
                orphan,
                orphan with { Transform = new Transform3D(Basis.Identity, new Vector3(1, 1, 1)) }
            )
        );

        Assert.Equal(0, changedCount);
        Assert.Equal(serialBefore, document.MutationSerial);
        Assert.Equal(0, document.ObjectCount);
    }

    [Fact]
    public void ReplaceObjectRejectsMismatchedIdsWithoutMutationOrEvent()
    {
        using var document = new Document();
        MeshObject expected = document.Add("Object", Transform3D.Identity, null!);
        var otherId = new MeshObject(
            new MeshObjectId(expected.Id.Value + 1),
            expected.Name,
            expected.Transform,
            expected.Mesh
        );
        int changedCount = 0;
        document.Changed += _ => changedCount++;
        long serialBefore = document.MutationSerial;

        Assert.False(document.ReplaceObject(expected, otherId));

        Assert.Equal(0, changedCount);
        Assert.Equal(serialBefore, document.MutationSerial);
        Assert.True(document.TryGetObject(expected.Id, out MeshObject current));
        Assert.Same(expected, current);
    }

    [Fact]
    public void ReplaceObjectRejectsMeshReferenceChangesWithoutMutationOrEvent()
    {
        using var document = new Document();
        MeshObject expected = document.Add("Object", Transform3D.Identity, null!);
        var differentMesh = new MeshObject(
            expected.Id,
            expected.Name,
            expected.Transform,
            (GDMesh)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(
                typeof(GDMesh)
            )
        );
        int changedCount = 0;
        document.Changed += _ => changedCount++;
        long serialBefore = document.MutationSerial;

        Assert.False(document.ReplaceObject(expected, differentMesh));

        Assert.Equal(0, changedCount);
        Assert.Equal(serialBefore, document.MutationSerial);
        Assert.True(document.TryGetObject(expected.Id, out MeshObject current));
        Assert.Same(expected, current);
    }

    [Fact]
    public void RemoveUnknownIdIsNoOp()
    {
        using var document = new Document();
        int changedCount = 0;
        document.Changed += _ => changedCount++;
        long serialBefore = document.MutationSerial;

        MeshObject? removed = document.Remove(new MeshObjectId(42));

        Assert.Null(removed);
        Assert.Equal(0, changedCount);
        Assert.Equal(serialBefore, document.MutationSerial);
        Assert.Equal(0, document.ObjectCount);
        Assert.Empty(document.GetObjects());
    }

    [Fact]
    public void PreviouslyReturnedObjectDoesNotChangeWhenReplacementIsInstalled()
    {
        using var document = new Document();
        MeshObject before = document.Add("Object", Transform3D.Identity, null!);
        var updated = new Transform3D(Basis.Identity, new Vector3(7, 8, 9));
        MeshObject replacement = before with { Transform = updated };

        Assert.True(document.ReplaceObject(before, replacement));

        Assert.Equal(Transform3D.Identity, before.Transform);
        Assert.True(document.TryGetObject(before.Id, out MeshObject after));
        Assert.Equal(updated, after.Transform);
        Assert.NotSame(before, after);
        Assert.Same(replacement, after);
        Assert.Equal(before.Id, after.Id);
        Assert.Equal(before.Name, after.Name);
        Assert.Same(before.Mesh, after.Mesh);
    }

    [Fact]
    public void ObjectCollectionSnapshotsDoNotFollowLaterDocumentChanges()
    {
        using var document = new Document();
        MeshObject first = document.Add("First", Transform3D.Identity, null!);

        MeshObject[] snapshot = document.GetObjects();
        document.Add("Second", Transform3D.Identity, null!);
        Assert.True(
            document.ReplaceObject(
                first,
                first with { Transform = new Transform3D(Basis.Identity, new Vector3(1, 0, 0)) }
            )
        );

        Assert.Single(snapshot);
        Assert.Same(first, snapshot[0]);
        Assert.Equal(2, document.ObjectCount);
        Assert.Equal(Transform3D.Identity, snapshot[0].Transform);
    }

    [Fact]
    public void MeshObjectPropertiesAreInitOnlyAndWithDoesNotMutatePriorInstances()
    {
        string[] expected = ["Id", "Name", "Transform", "Mesh"];
        foreach (string propertyName in expected)
        {
            PropertyInfo property = typeof(MeshObject).GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
            )!;
            MethodInfo setter = property.GetSetMethod(nonPublic: true)!;
            Assert.Contains(
                setter.ReturnParameter.GetRequiredCustomModifiers(),
                modifier => modifier.Name == "IsExternalInit"
            );
        }

        var original = new MeshObject(
            new MeshObjectId(1),
            "Name",
            Transform3D.Identity,
            null!
        );
        MeshObject modified = original with
        {
            Name = "Other",
            Transform = new Transform3D(Basis.Identity, new Vector3(1, 2, 3)),
        };

        Assert.Equal("Name", original.Name);
        Assert.Equal(Transform3D.Identity, original.Transform);
        Assert.Equal("Other", modified.Name);
        Assert.NotSame(original, modified);
    }

    [Fact]
    public void GetObjectsDoesNotExposeTheLiveCollection()
    {
        using var document = new Document();
        document.Add("First", Transform3D.Identity, null!);

        MeshObject[] firstSnapshot = document.GetObjects();
        MeshObject[] secondSnapshot = document.GetObjects();

        Assert.NotSame(firstSnapshot, secondSnapshot);
        Assert.Equal(firstSnapshot, secondSnapshot);
    }

    [Fact]
    public void DisposeClearsObjectsAndRejectsFurtherMutations()
    {
        var document = new Document();
        document.Add("Object", Transform3D.Identity, null!);

        document.Dispose();
        document.Dispose();

        Assert.Equal(0, document.ObjectCount);
        Assert.Throws<ObjectDisposedException>(
            () => document.Add("Another", Transform3D.Identity, null!)
        );
        Assert.Throws<ObjectDisposedException>(() => document.GetObjects());
    }
}
