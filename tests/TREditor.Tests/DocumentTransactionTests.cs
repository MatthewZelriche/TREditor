using Godot;

namespace TREditor.Tests;

public sealed class DocumentTransactionTests
{
    [Fact]
    public void MutationSerialAdvancesOncePerSuccessfulOperation()
    {
        using var document = new Document();
        Assert.Equal(0, document.MutationSerial);

        MeshObject first = document.Add("First", Transform3D.Identity, null!);
        Assert.Equal(1, document.MutationSerial);

        MeshObject second = document.Add("Second", Transform3D.Identity, null!);
        Assert.Equal(2, document.MutationSerial);

        Assert.True(
            document.ReplaceObject(
                first,
                first with { Transform = new Transform3D(Basis.Identity, new Vector3(1, 0, 0)) }
            )
        );
        Assert.Equal(3, document.MutationSerial);

        Assert.True(document.CommitGeometryChange(second.Id));
        Assert.Equal(4, document.MutationSerial);

        Assert.NotNull(document.Remove(second.Id));
        Assert.Equal(5, document.MutationSerial);
    }

    [Fact]
    public void SuccessfulOperationsRaiseExactlyOneChangedEventWithSerialAndKinds()
    {
        using var document = new Document();
        var changeSets = new List<DocumentChangeSet>();
        document.Changed += changeSets.Add;

        MeshObject meshObject = document.Add("Object", Transform3D.Identity, null!);
        Assert.Single(changeSets);
        Assert.Equal(1, changeSets[0].MutationSerial);
        Assert.Equal(DocumentChangeKind.Added, changeSets[0].Change.Kind);
        Assert.Equal(meshObject.Id, changeSets[0].Change.ObjectId);

        changeSets.Clear();
        MeshObject replacement = meshObject with
        {
            Transform = new Transform3D(Basis.Identity, new Vector3(2, 3, 4)),
        };
        Assert.True(document.ReplaceObject(meshObject, replacement));
        Assert.Single(changeSets);
        Assert.Equal(2, changeSets[0].MutationSerial);
        Assert.Equal(DocumentChangeKind.TransformChanged, changeSets[0].Change.Kind);

        changeSets.Clear();
        Assert.True(document.CommitGeometryChange(meshObject.Id));
        Assert.Single(changeSets);
        Assert.Equal(3, changeSets[0].MutationSerial);
        Assert.Equal(DocumentChangeKind.GeometryChanged, changeSets[0].Change.Kind);

        changeSets.Clear();
        Assert.NotNull(document.Remove(meshObject.Id));
        Assert.Single(changeSets);
        Assert.Equal(4, changeSets[0].MutationSerial);
        Assert.Equal(DocumentChangeKind.Removed, changeSets[0].Change.Kind);
    }

    [Fact]
    public void RejectedReplaceObjectLeavesSerialAndEventsUnchanged()
    {
        using var document = new Document();
        MeshObject original = document.Add("Object", Transform3D.Identity, null!);
        MeshObject installed = original with
        {
            Transform = new Transform3D(Basis.Identity, new Vector3(1, 0, 0)),
        };
        Assert.True(document.ReplaceObject(original, installed));
        long serialBefore = document.MutationSerial;
        int changedCount = 0;
        document.Changed += _ => changedCount++;

        Assert.False(
            document.ReplaceObject(
                original,
                original with { Transform = new Transform3D(Basis.Identity, new Vector3(9, 9, 9)) }
            )
        );

        Assert.Equal(0, changedCount);
        Assert.Equal(serialBefore, document.MutationSerial);
        Assert.True(document.TryGetObject(original.Id, out MeshObject current));
        Assert.Same(installed, current);
    }

    [Fact]
    public void RemoveReturnsTheObjectWithoutDisposingItsMesh()
    {
        using var document = new Document();
        GDMesh? mesh = null;
        MeshObject meshObject = document.Add("Object", Transform3D.Identity, mesh!);

        MeshObject? removed = document.Remove(meshObject.Id);

        Assert.NotNull(removed);
        Assert.Same(meshObject.Mesh, removed.Mesh);
        Assert.False(document.TryGetObject(meshObject.Id, out _));
        Assert.Equal(0, document.ObjectCount);
    }

    [Fact]
    public void FailedAddBeforePublishLeavesSerialUnchanged()
    {
        using var document = new Document();
        long serialBefore = document.MutationSerial;
        int changedCount = 0;
        document.Changed += _ => changedCount++;

        Assert.Throws<ArgumentNullException>(() => document.Add(null!, Transform3D.Identity, null!));

        Assert.Equal(0, changedCount);
        Assert.Equal(serialBefore, document.MutationSerial);
        Assert.Equal(0, document.ObjectCount);
    }

    [Fact]
    public void RemoveUnknownReturnsNullWithoutAdvancingSerial()
    {
        using var document = new Document();
        document.Add("Object", Transform3D.Identity, null!);
        long serialBefore = document.MutationSerial;
        int changedCount = 0;
        document.Changed += _ => changedCount++;

        Assert.Null(document.Remove(new MeshObjectId(99)));

        Assert.Equal(0, changedCount);
        Assert.Equal(serialBefore, document.MutationSerial);
        Assert.Equal(1, document.ObjectCount);
    }

    [Fact]
    public void CommitGeometryChangeRejectsUnknownIdsWithoutAdvancingSerial()
    {
        using var document = new Document();
        long serialBefore = document.MutationSerial;
        int changedCount = 0;
        document.Changed += _ => changedCount++;

        Assert.False(document.CommitGeometryChange(new MeshObjectId(7)));

        Assert.Equal(0, changedCount);
        Assert.Equal(serialBefore, document.MutationSerial);
    }

    [Fact]
    public void ChangedPayloadContainsIdsOnlyAfterCommit()
    {
        using var document = new Document();
        DocumentChangeSet? observed = null;
        document.Changed += changeSet =>
        {
            observed = changeSet;
            Assert.True(document.TryGetObject(changeSet.Change.ObjectId, out _));
        };

        MeshObject meshObject = document.Add("Object", Transform3D.Identity, null!);

        Assert.NotNull(observed);
        Assert.Equal(meshObject.Id, observed.Change.ObjectId);
        Assert.Equal(DocumentChangeKind.Added, observed.Change.Kind);
    }
}
