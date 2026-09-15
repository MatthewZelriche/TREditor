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
            Document source = application.CurrentDocument!;
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

            application.SaveScene(scenePath);
            application.NewScene();
            AssertThat(application.Document?.ObjectCount ?? -1).IsEqual(0);
            AssertThat(application.CurrentFilePath).IsNull();

            application.OpenScene(scenePath);

            Document loaded = application.CurrentDocument!;
            AssertThat(loaded).IsNotNull();
            AssertThat(application.CurrentFilePath).IsEqual(scenePath);
            AssertThat(application.Document!.ObjectCount).IsEqual(2);

            MeshObject cube = loaded.GetObjects().Single(value => value.Name == "Cube");
            AssertThat(cube.Transform).IsEqual(cubeTransform);
            AssertThat(cube.Mesh.SpatialMesh.VertexCount).IsEqual(8);
            AssertThat(cube.Mesh.SpatialMesh.FaceCount).IsEqual(6);

            MeshObject cylinder = loaded.GetObjects().Single(value => value.Name == "Cylinder");
            AssertThat(cylinder.Transform).IsEqual(cylinderTransform);
            AssertThat(cylinder.Mesh.SpatialMesh.VertexCount).IsEqual(16);
            AssertThat(cylinder.Mesh.SpatialMesh.FaceCount).IsEqual(10);

            application.SaveScene(scenePath);
            application.Update(0.0);
        }
        finally
        {
            RemoveSceneFile();
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DocumentCommandsMutateTheDocumentAndAreReversible()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        Document document = application.CurrentDocument!;
        var beforeTransform = new Transform3D(Basis.Identity, new Vector3(1, 1, 1));
        var afterTransform = new Transform3D(Basis.Identity, new Vector3(4, 5, 6));
        MeshObject before = document.Add(
            "Movable",
            beforeTransform,
            new GDMesh(SpatialMeshBuilder.CreateAabb(NumericsVector3.Zero, NumericsVector3.One))
        );
        MeshObject after = before with { Transform = afterTransform };

        application.Commands.Submit(new MoveCommand(before, after));

        AssertBool(application.Document!.TryGetMeshTransform(before.Id, out Transform3D moved)).IsTrue();
        AssertThat(moved).IsEqual(afterTransform);
        AssertBool(application.CanUndo).IsTrue();

        AssertBool(application.Undo()).IsTrue();
        AssertBool(application.Document!.TryGetMeshTransform(before.Id, out Transform3D undone)).IsTrue();
        AssertThat(undone).IsEqual(beforeTransform);

        AssertBool(application.Redo()).IsTrue();
        AssertBool(application.Document!.TryGetMeshTransform(before.Id, out Transform3D redone)).IsTrue();
        AssertThat(redone).IsEqual(afterTransform);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DocumentReplacementRetargetsTheDocumentCommandRoute()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        Document previousDocument = application.CurrentDocument!;
        MeshObject stale = previousDocument.Add(
            "Stale",
            Transform3D.Identity,
            new GDMesh(SpatialMeshBuilder.CreateAabb(NumericsVector3.Zero, NumericsVector3.One))
        );

        application.NewScene();

        AssertThrown(() => application.Commands.Submit(
            new MoveCommand(stale, stale with { Transform = Transform3D.Identity })
        )).IsInstanceOf<InvalidOperationException>();
        AssertBool(application.CanUndo).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DocumentReaderFollowsDocumentReplacement()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        Document initialDocument = application.CurrentDocument!;
        var transform = new Transform3D(Basis.Identity, new Vector3(7, 8, 9));
        MeshObject added = initialDocument.Add(
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

            application.SaveScene(scenePath);
            application.SaveScene(scenePath);

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
            application.SaveScene(scenePath);

            Document previousDocument = application.CurrentDocument!;
            var disposeLog = new List<string>();
            application.Commands.Submit(
                new DocumentAwareCommand("Edit", previousDocument, disposeLog)
            );
            AssertBool(application.CanUndo).IsTrue();

            application.NewScene();

            AssertBool(application.CanUndo).IsFalse();
            AssertBool(application.CanRedo).IsFalse();
            AssertThat(string.Join(",", disposeLog)).IsEqual("dispose-while-alive:Edit");
            AssertBool(IsDocumentDisposed(previousDocument)).IsTrue();

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
        Document documentBeforeFailure = application.CurrentDocument!;
        int changedCount = 0;
        application.CommandHistoryChanged += () => changedCount++;

        Exception? failure = null;
        application.OperationFailed += (_, exception) => failure = exception;
        application.OpenScene(
            ProjectSettings.GlobalizePath("user://application_missing_scene.res")
        );

        AssertThat(failure).IsInstanceOf<NonFatalSceneError>();
        AssertThat(changedCount).IsEqual(0);
        AssertBool(application.CanUndo).IsTrue();
        AssertThat(application.UndoDisplayName).IsEqual("Append kept");
        AssertBool(ReferenceEquals(documentBeforeFailure, application.CurrentDocument)).IsTrue();
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
    public void ApplicationDisposalReleasesCommandsBeforeTheCurrentDocument()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        var application = new Application(mapRoot);
        application.NewScene();
        Document document = application.CurrentDocument!;
        var disposeLog = new List<string>();
        application.Commands.Submit(new DocumentAwareCommand("Owned", document, disposeLog));

        application.Dispose();

        AssertThat(string.Join(",", disposeLog)).IsEqual("dispose-while-alive:Owned");
        AssertBool(IsDocumentDisposed(document)).IsTrue();
    }

    private static bool IsDocumentDisposed(Document document)
    {
        try
        {
            document.CommitGeometryChange(new MeshObjectId(0));
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

        public void Execute(Document _) => document.Add(value);

        public void Undo(Document _) => document.RemoveAt(document.Count - 1);
    }

    private sealed class MoveCommand(MeshObject before, MeshObject after) : IEditorCommand
    {
        public string DisplayName => "Move";

        public void Execute(Document document)
        {
            if (!document.ReplaceObject(before, after))
                throw new InvalidOperationException("The target object no longer exists.");
        }

        public void Undo(Document document)
        {
            if (!document.ReplaceObject(after, before))
                throw new InvalidOperationException("The target object no longer exists.");
        }
    }

    private sealed class FailingCommand : IEditorCommand, IDisposable
    {
        public string DisplayName => "Failing";

        public int DisposeCount { get; private set; }

        public void Execute(Document _) =>
            throw new InvalidOperationException("Failing could not be executed.");

        public void Undo(Document _)
        {
        }

        public void Dispose() => DisposeCount++;
    }

    private sealed class DocumentAwareCommand(
        string displayName,
        Document document,
        List<string> disposeLog
    ) : IEditorCommand, IDisposable
    {
        public string DisplayName => displayName;

        public void Execute(Document _)
        {
        }

        public void Undo(Document _)
        {
        }

        public void Dispose()
        {
            // Throws ObjectDisposedException if the history released this command after the document.
            document.CommitGeometryChange(new MeshObjectId(0));
            disposeLog.Add($"dispose-while-alive:{displayName}");
        }
    }
}
