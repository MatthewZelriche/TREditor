using System;
using Godot;

namespace TREditor.Tests;

public class SceneCommandTests
{
    private static readonly Transform3D Before = new(Basis.Identity, new Vector3(0, 0, 0));
    private static readonly Transform3D After = new(Basis.Identity, new Vector3(1, 2, 3));

    [Fact]
    public void SubmitExecutesUndoesAndRedoesAgainstTheScene()
    {
        using var scene = new Scene();
        using var history = new CommandHistory();
        history.SetScene(scene);
        IEditorCommandSink sink = history;
        Scene.MeshObject target = scene.Add("Movable", Before, null!);

        sink.Submit(new MoveCommand(target.Id, Before, After));
        Assert.Equal(After, ReadTransform(scene, target.Id));

        Assert.True(history.Undo());
        Assert.Equal(Before, ReadTransform(scene, target.Id));

        Assert.True(history.Redo());
        Assert.Equal(After, ReadTransform(scene, target.Id));
    }

    [Fact]
    public void CommittingAgainstAMissingObjectFailsAndLeavesTheHistoryUnchanged()
    {
        using var scene = new Scene();
        using var history = new CommandHistory();
        history.SetScene(scene);

        Assert.Throws<InvalidOperationException>(
            () => history.Submit(new MoveCommand(new MeshObjectId(7), Before, After))
        );

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void CommandsRunAgainstTheSceneTheHistoryCurrentlyHolds()
    {
        using var first = new Scene();
        using var second = new Scene();
        using var history = new CommandHistory();
        history.SetScene(first);
        Scene.MeshObject inFirst = first.Add("Movable", Before, null!);
        Scene.MeshObject inSecond = second.Add("Movable", Before, null!);

        history.Submit(new MoveCommand(inFirst.Id, Before, After));
        history.Clear();
        history.SetScene(second);
        history.Submit(new MoveCommand(inSecond.Id, Before, After));

        Assert.Equal(After, ReadTransform(first, inFirst.Id));
        Assert.Equal(After, ReadTransform(second, inSecond.Id));
    }

    [Fact]
    public void OperationsWithoutASceneFail()
    {
        using var history = new CommandHistory();

        Assert.Throws<InvalidOperationException>(
            () => history.Submit(new MoveCommand(new MeshObjectId(0), Before, After))
        );
    }

    private static Transform3D ReadTransform(Scene scene, MeshObjectId id)
    {
        Assert.True(scene.TryGetMeshTransform(id, out Transform3D transform));
        return transform;
    }

    private sealed class MoveCommand(MeshObjectId id, Transform3D before, Transform3D after)
        : IEditorCommand
    {
        public string DisplayName => "Move";

        public void Execute(Scene scene) => Apply(scene, after);

        public void Undo(Scene scene) => Apply(scene, before);

        private void Apply(Scene scene, Transform3D transform)
        {
            if (!scene.SetTransform(id, transform))
                throw new InvalidOperationException("The target object no longer exists.");
        }
    }
}
