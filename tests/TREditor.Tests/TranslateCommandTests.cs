using System;
using Godot;

namespace TREditor.Tests;

public sealed class TranslateCommandTests
{
    private static readonly Transform3D BeforeTransform = new(Basis.Identity, new Vector3(1, 0, -2));
    private static readonly Transform3D AfterTransform = new(Basis.Identity, new Vector3(4, 5, -2));

    [Fact]
    public void ExecuteUndoAndRedoApplyTheExactAbsoluteTransforms()
    {
        using var document = new Document();
        using var history = new CommandHistory();
        history.SetDocument(document);
        MeshObject before = document.Add("Movable", BeforeTransform, null!);
        MeshObject after = before with { Transform = AfterTransform };

        history.Submit(new TranslateCommand(before, after));
        Assert.Equal(AfterTransform, ReadTransform(document, before.Id));

        Assert.True(history.Undo());
        Assert.Equal(BeforeTransform, ReadTransform(document, before.Id));

        Assert.True(history.Redo());
        Assert.Equal(AfterTransform, ReadTransform(document, before.Id));
    }

    [Fact]
    public void RepeatedExecuteUndoRedoWithTheSameRecordsSucceeds()
    {
        using var document = new Document();
        using var history = new CommandHistory();
        history.SetDocument(document);
        MeshObject before = document.Add("Movable", BeforeTransform, null!);
        MeshObject after = before with { Transform = AfterTransform };
        var command = new TranslateCommand(before, after);

        history.Submit(command);
        Assert.True(history.Undo());
        Assert.True(history.Redo());
        Assert.True(history.Undo());
        Assert.True(history.Redo());

        Assert.Equal(AfterTransform, ReadTransform(document, before.Id));
        Assert.True(document.TryGetObject(before.Id, out MeshObject current));
        Assert.Same(after, current);
    }

    [Fact]
    public void InterveningReplaceObjectMakesRedoFailWithoutMovingStacks()
    {
        using var document = new Document();
        using var history = new CommandHistory();
        history.SetDocument(document);
        MeshObject before = document.Add("Movable", BeforeTransform, null!);
        MeshObject after = before with { Transform = AfterTransform };
        history.Submit(new TranslateCommand(before, after));
        Assert.True(history.Undo());

        MeshObject intervening = before with
        {
            Transform = new Transform3D(Basis.Identity, new Vector3(9, 9, 9)),
        };
        Assert.True(document.ReplaceObject(before, intervening));

        Assert.False(history.Redo());
        Assert.False(history.CanUndo);
        Assert.True(history.CanRedo);
        Assert.True(document.TryGetObject(before.Id, out MeshObject current));
        Assert.Same(intervening, current);
    }

    [Fact]
    public void StaleExpectedOnExecuteFailsWithoutEnteringHistory()
    {
        using var document = new Document();
        using var history = new CommandHistory();
        history.SetDocument(document);
        MeshObject before = document.Add("Movable", BeforeTransform, null!);
        MeshObject after = before with { Transform = AfterTransform };
        MeshObject intervening = before with
        {
            Transform = new Transform3D(Basis.Identity, new Vector3(3, 3, 3)),
        };
        Assert.True(document.ReplaceObject(before, intervening));

        string? failedOperation = null;
        Exception? failedException = null;
        history.OperationFailed += (operation, exception) =>
        {
            failedOperation = operation;
            failedException = exception;
        };

        history.Submit(new TranslateCommand(before, after));
        Assert.Equal("Translate", failedOperation);
        Assert.IsType<NonFatalCommandError>(failedException);
        Assert.False(history.CanUndo);
        Assert.True(document.TryGetObject(before.Id, out MeshObject current));
        Assert.Same(intervening, current);
    }

    [Fact]
    public void StaleExpectedOnUndoFailsWithoutMovingStacks()
    {
        using var document = new Document();
        using var history = new CommandHistory();
        history.SetDocument(document);
        MeshObject before = document.Add("Movable", BeforeTransform, null!);
        MeshObject after = before with { Transform = AfterTransform };
        history.Submit(new TranslateCommand(before, after));

        MeshObject intervening = after with
        {
            Transform = new Transform3D(Basis.Identity, new Vector3(8, 8, 8)),
        };
        Assert.True(document.ReplaceObject(after, intervening));

        string? failedOperation = null;
        Exception? failedException = null;
        history.OperationFailed += (operation, exception) =>
        {
            failedOperation = operation;
            failedException = exception;
        };

        Assert.False(history.Undo());
        Assert.Equal("Undo", failedOperation);
        Assert.IsType<NonFatalCommandError>(failedException);
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.True(document.TryGetObject(before.Id, out MeshObject current));
        Assert.Same(intervening, current);
    }

    [Fact]
    public void TranslatingAMissingObjectFails()
    {
        using var document = new Document();
        using var history = new CommandHistory();
        history.SetDocument(document);
        var before = new MeshObject(new MeshObjectId(12), "Missing", BeforeTransform, null!);
        MeshObject after = before with { Transform = AfterTransform };

        history.Submit(new TranslateCommand(before, after));
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void UndoingAgainstARemovedObjectFails()
    {
        using var document = new Document();
        using var history = new CommandHistory();
        history.SetDocument(document);
        MeshObject before = document.Add("Movable", BeforeTransform, null!);
        MeshObject after = before with { Transform = AfterTransform };
        history.Submit(new TranslateCommand(before, after));

        MeshObject? removed = document.Remove(before.Id);
        Assert.NotNull(removed);
        removed.Mesh?.Dispose();

        Assert.False(history.Undo());
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void TheDisplayNameDescribesTheMove()
    {
        var before = new MeshObject(new MeshObjectId(0), "Name", BeforeTransform, null!);
        MeshObject after = before with { Transform = AfterTransform };
        Assert.Equal("Translate", new TranslateCommand(before, after).DisplayName);
    }

    private static Transform3D ReadTransform(Document document, MeshObjectId id)
    {
        Assert.True(document.TryGetMeshTransform(id, out Transform3D transform));
        return transform;
    }
}
