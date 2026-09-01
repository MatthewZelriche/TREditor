using System;
using Godot;

namespace TREditor.Tests;

public sealed class TranslateCommandTests
{
    private static readonly Transform3D Before = new(Basis.Identity, new Vector3(1, 0, -2));
    private static readonly Transform3D After = new(Basis.Identity, new Vector3(4, 5, -2));

    [Fact]
    public void ExecuteUndoAndRedoApplyTheExactAbsoluteTransforms()
    {
        using var scene = new Scene();
        using var history = new CommandHistory();
        history.SetScene(scene);
        Scene.MeshObject target = scene.Add("Movable", Before, null!);

        history.Submit(new TranslateCommand(target.Id, Before, After));
        Assert.Equal(After, ReadTransform(scene, target.Id));

        Assert.True(history.Undo());
        Assert.Equal(Before, ReadTransform(scene, target.Id));

        Assert.True(history.Redo());
        Assert.Equal(After, ReadTransform(scene, target.Id));
    }

    [Fact]
    public void RedoIsUnaffectedByTransformsAppliedInBetween()
    {
        using var scene = new Scene();
        using var history = new CommandHistory();
        history.SetScene(scene);
        Scene.MeshObject target = scene.Add("Movable", Before, null!);
        history.Submit(new TranslateCommand(target.Id, Before, After));
        Assert.True(history.Undo());

        Assert.True(
            scene.SetTransform(target.Id, new Transform3D(Basis.Identity, new Vector3(9, 9, 9)))
        );
        Assert.True(history.Redo());

        Assert.Equal(After, ReadTransform(scene, target.Id));
    }

    [Fact]
    public void TranslatingAMissingObjectFails()
    {
        using var scene = new Scene();
        using var history = new CommandHistory();
        history.SetScene(scene);

        Assert.Throws<InvalidOperationException>(
            () => history.Submit(new TranslateCommand(new MeshObjectId(12), Before, After))
        );
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void UndoingAgainstARemovedObjectFails()
    {
        using var scene = new Scene();
        using var history = new CommandHistory();
        history.SetScene(scene);
        Scene.MeshObject target = scene.Add("Movable", Before, null!);
        history.Submit(new TranslateCommand(target.Id, Before, After));

        Assert.True(scene.Remove(target.Id));

        Assert.Throws<InvalidOperationException>(() => history.Undo());
    }

    [Fact]
    public void TheDisplayNameDescribesTheMove()
    {
        Assert.Equal("Translate", new TranslateCommand(new MeshObjectId(0), Before, After).DisplayName);
    }

    private static Transform3D ReadTransform(Scene scene, MeshObjectId id)
    {
        Assert.True(scene.TryGetMeshTransform(id, out Transform3D transform));
        return transform;
    }
}
