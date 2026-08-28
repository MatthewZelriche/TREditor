using System;
using System.Collections.Generic;
using System.Linq;
using GdUnit4;
using Godot;
using TRMesh.Mesh;
using static GdUnit4.Assertions;
using NumericsVector3 = System.Numerics.Vector3;

namespace TREditor.GodotTests;

[TestSuite]
public class ApplicationTests
{
    private const string ScenePath = "user://application_round_trip.res";

    [TestCase]
    [RequireGodotRuntime]
    public void ApplicationSavesAndOpensItsCurrentScene()
    {
        RemoveSceneFile();
        try
        {
            Node3D mapRoot = AutoFree(new Node3D());
            using var application = new Application(mapRoot);
            application.NewScene();
            string scenePath = ProjectSettings.GlobalizePath(ScenePath);
            var cubeTransform = new Transform3D(Basis.Identity, new Vector3(1, 2, 3));
            var cylinderTransform = new Transform3D(Basis.Identity, new Vector3(-4, 5, -6));
            Scene source = application.CurrentScene!;
            AssertThat(source).IsNotNull();
            source.Add(
                "Cube",
                cubeTransform,
                new GDMesh(SpatialMeshBuilder.CreateAabb(NumericsVector3.Zero, NumericsVector3.One))
            );
            source.Add(
                "Cylinder",
                cylinderTransform,
                new GDMesh(SpatialMeshBuilder.CreateCylinder(2f, 1f, 8))
            );

            application.SaveSceneAs(scenePath);
            application.NewScene();
            AssertThat(application.Document?.ObjectCount ?? -1).IsEqual(0);
            AssertThat(application.CurrentFilePath).IsNull();

            application.OpenScene(scenePath);

            Scene loaded = application.CurrentScene!;
            AssertThat(loaded).IsNotNull();
            AssertThat(application.CurrentFilePath).IsEqual(scenePath);
            AssertThat(application.Document!.ObjectCount).IsEqual(2);

            Scene.MeshObject cube = loaded.Objects.Values.Single(value => value.Name == "Cube");
            AssertThat(cube.Transform).IsEqual(cubeTransform);
            AssertThat(cube.Mesh.SpatialMesh.VertexCount).IsEqual(8);
            AssertThat(cube.Mesh.SpatialMesh.FaceCount).IsEqual(6);

            Scene.MeshObject cylinder = loaded.Objects.Values.Single(value => value.Name == "Cylinder");
            AssertThat(cylinder.Transform).IsEqual(cylinderTransform);
            AssertThat(cylinder.Mesh.SpatialMesh.VertexCount).IsEqual(16);
            AssertThat(cylinder.Mesh.SpatialMesh.FaceCount).IsEqual(10);

            application.SaveScene();
            application.Update(0.0);
        }
        finally
        {
            RemoveSceneFile();
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DocumentCommandsMutateTheSceneAndAreReversible()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        Scene scene = application.CurrentScene!;
        var before = new Transform3D(Basis.Identity, new Vector3(1, 1, 1));
        var after = new Transform3D(Basis.Identity, new Vector3(4, 5, 6));
        Scene.MeshObject target = scene.Add(
            "Movable",
            before,
            new GDMesh(SpatialMeshBuilder.CreateAabb(NumericsVector3.Zero, NumericsVector3.One))
        );

        application.Commands.Submit(new MoveCommand(target.Id, before, after));

        AssertBool(application.Document!.TryGetMeshTransform(target.Id, out Transform3D moved)).IsTrue();
        AssertThat(moved).IsEqual(after);
        AssertBool(application.CanUndo).IsTrue();

        AssertBool(application.Undo()).IsTrue();
        AssertBool(application.Document!.TryGetMeshTransform(target.Id, out Transform3D undone)).IsTrue();
        AssertThat(undone).IsEqual(before);

        AssertBool(application.Redo()).IsTrue();
        AssertBool(application.Document!.TryGetMeshTransform(target.Id, out Transform3D redone)).IsTrue();
        AssertThat(redone).IsEqual(after);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void SceneReplacementRetargetsTheDocumentCommandRoute()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        Scene previousScene = application.CurrentScene!;
        Scene.MeshObject stale = previousScene.Add(
            "Stale",
            Transform3D.Identity,
            new GDMesh(SpatialMeshBuilder.CreateAabb(NumericsVector3.Zero, NumericsVector3.One))
        );

        application.NewScene();

        AssertThrown(() => application.Commands.Submit(
            new MoveCommand(stale.Id, Transform3D.Identity, Transform3D.Identity)
        )).IsInstanceOf<InvalidOperationException>();
        AssertBool(application.CanUndo).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DocumentReaderFollowsSceneReplacement()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        Scene initialScene = application.CurrentScene!;
        var transform = new Transform3D(Basis.Identity, new Vector3(7, 8, 9));
        Scene.MeshObject added = initialScene.Add(
            "Tracked",
            transform,
            new GDMesh(SpatialMeshBuilder.CreateAabb(NumericsVector3.Zero, NumericsVector3.One))
        );

        IEditorDocumentReader readerBefore = application.Document!;
        AssertBool(readerBefore.TryGetMeshTransform(added.Id, out Transform3D readTransform)).IsTrue();
        AssertThat(readTransform).IsEqual(transform);

        application.NewScene();

        IEditorDocumentReader readerAfter = application.Document!;
        AssertBool(ReferenceEquals(readerBefore, readerAfter)).IsFalse();
        AssertThat(readerAfter.ObjectCount).IsEqual(0);
        AssertBool(readerAfter.TryGetMeshTransform(added.Id, out _)).IsFalse();
        AssertThat(readTransform).IsEqual(transform);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ApplicationExecutesUndoesAndRedoesCommands()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        var document = new List<string>();
        int changedCount = 0;
        application.CommandHistoryChanged += () => changedCount++;

        application.Commands.Submit(new AppendCommand(document, "first"));
        application.Commands.Submit(new AppendCommand(document, "second"));

        AssertThat(string.Join(",", document)).IsEqual("first,second");
        AssertThat(changedCount).IsEqual(2);
        AssertBool(application.CanUndo).IsTrue();
        AssertBool(application.CanRedo).IsFalse();
        AssertThat(application.UndoDisplayName).IsEqual("Append second");
        AssertThat(application.RedoDisplayName).IsNull();

        AssertBool(application.Undo()).IsTrue();
        AssertThat(string.Join(",", document)).IsEqual("first");
        AssertThat(application.UndoDisplayName).IsEqual("Append first");
        AssertThat(application.RedoDisplayName).IsEqual("Append second");

        AssertBool(application.Redo()).IsTrue();
        AssertThat(string.Join(",", document)).IsEqual("first,second");
        AssertThat(changedCount).IsEqual(4);
        AssertBool(application.CanRedo).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void SaveAndSaveAsPreserveCommandHistory()
    {
        RemoveSceneFile();
        try
        {
            Node3D mapRoot = AutoFree(new Node3D());
            using var application = new Application(mapRoot);
            application.NewScene();
            string scenePath = ProjectSettings.GlobalizePath(ScenePath);
            var document = new List<string>();
            application.Commands.Submit(new AppendCommand(document, "edit"));
            AssertBool(application.Undo()).IsTrue();
            int changedCount = 0;
            application.CommandHistoryChanged += () => changedCount++;

            application.SaveSceneAs(scenePath);
            application.SaveScene();

            AssertThat(changedCount).IsEqual(0);
            AssertBool(application.CanUndo).IsFalse();
            AssertBool(application.CanRedo).IsTrue();
            AssertThat(application.RedoDisplayName).IsEqual("Append edit");
            AssertThat(document.Count).IsEqual(0);
        }
        finally
        {
            RemoveSceneFile();
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void NewAndOpenClearHistoryOnlyAfterSuccessfulReplacement()
    {
        RemoveSceneFile();
        try
        {
            Node3D mapRoot = AutoFree(new Node3D());
            using var application = new Application(mapRoot);
            application.NewScene();
            string scenePath = ProjectSettings.GlobalizePath(ScenePath);
            application.SaveSceneAs(scenePath);

            Scene previousScene = application.CurrentScene!;
            var disposeLog = new List<string>();
            application.Commands.Submit(
                new SceneAwareCommand("Edit", previousScene, disposeLog)
            );
            AssertBool(application.CanUndo).IsTrue();

            application.NewScene();

            AssertBool(application.CanUndo).IsFalse();
            AssertBool(application.CanRedo).IsFalse();
            AssertThat(string.Join(",", disposeLog)).IsEqual("dispose-while-alive:Edit");
            AssertBool(IsSceneDisposed(previousScene)).IsTrue();

            application.Commands.Submit(new AppendCommand([], "before-open"));
            AssertBool(application.CanUndo).IsTrue();
            application.OpenScene(scenePath);

            AssertBool(application.CanUndo).IsFalse();
            AssertBool(application.CanRedo).IsFalse();
            AssertThat(application.CurrentFilePath).IsEqual(scenePath);
        }
        finally
        {
            RemoveSceneFile();
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void FailedOpenPreservesCommandHistory()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        application.NewScene();
        var document = new List<string>();
        application.Commands.Submit(new AppendCommand(document, "kept"));
        Scene sceneBeforeFailure = application.CurrentScene!;
        int changedCount = 0;
        application.CommandHistoryChanged += () => changedCount++;

        Exception? failure = null;
        try
        {
            application.OpenScene(
                ProjectSettings.GlobalizePath("user://application_missing_scene.res")
            );
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        AssertThat(failure).IsNotNull();
        AssertThat(changedCount).IsEqual(0);
        AssertBool(application.CanUndo).IsTrue();
        AssertThat(application.UndoDisplayName).IsEqual("Append kept");
        AssertBool(ReferenceEquals(sceneBeforeFailure, application.CurrentScene)).IsTrue();
        AssertThat(string.Join(",", document)).IsEqual("kept");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void FailedExecuteCommandLeavesHistoryUnchanged()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        application.Commands.Submit(new AppendCommand([], "applied"));
        int changedCount = 0;
        application.CommandHistoryChanged += () => changedCount++;
        var failing = new FailingCommand();

        Exception? failure = null;
        try
        {
            application.Commands.Submit(failing);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        AssertThat(failure).IsNotNull();
        AssertThat(changedCount).IsEqual(0);
        AssertThat(application.UndoDisplayName).IsEqual("Append applied");
        AssertBool(application.CanRedo).IsFalse();
        AssertThat(failing.DisposeCount).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ApplicationDisposalReleasesCommandsBeforeTheCurrentScene()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        var application = new Application(mapRoot);
        application.NewScene();
        Scene scene = application.CurrentScene!;
        var disposeLog = new List<string>();
        application.Commands.Submit(new SceneAwareCommand("Owned", scene, disposeLog));

        application.Dispose();

        AssertThat(string.Join(",", disposeLog)).IsEqual("dispose-while-alive:Owned");
        AssertBool(IsSceneDisposed(scene)).IsTrue();
    }

    private static bool IsSceneDisposed(Scene scene)
    {
        try
        {
            scene.MarkGeometryChanged(new MeshObjectId(0));
            return false;
        }
        catch (ObjectDisposedException)
        {
            return true;
        }
    }

    private static void RemoveSceneFile()
    {
        if (Godot.FileAccess.FileExists(ScenePath))
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(ScenePath));
    }

    private sealed class AppendCommand(List<string> document, string value) : IEditorCommand
    {
        public string DisplayName => $"Append {value}";

        public void Execute(Scene scene) => document.Add(value);

        public void Undo(Scene scene) => document.RemoveAt(document.Count - 1);
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

    private sealed class FailingCommand : IEditorCommand, IDisposable
    {
        public string DisplayName => "Failing";

        public int DisposeCount { get; private set; }

        public void Execute(Scene scene) =>
            throw new InvalidOperationException("Failing could not be executed.");

        public void Undo(Scene scene)
        {
        }

        public void Dispose() => DisposeCount++;
    }

    private sealed class SceneAwareCommand(
        string displayName,
        Scene scene,
        List<string> disposeLog
    ) : IEditorCommand, IDisposable
    {
        public string DisplayName => displayName;

        public void Execute(Scene scene)
        {
        }

        public void Undo(Scene scene)
        {
        }

        public void Dispose()
        {
            // Throws ObjectDisposedException if the history released this command after the scene.
            scene.MarkGeometryChanged(new MeshObjectId(0));
            disposeLog.Add($"dispose-while-alive:{displayName}");
        }
    }
}
