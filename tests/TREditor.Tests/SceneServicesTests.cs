using Godot;

namespace TREditor.Tests;

public sealed class SceneServicesTests
{
    [Fact]
    public void FansOutLifecycleAndDocumentEventsInRegistrationOrder()
    {
        using var document = new Document();
        var calls = new List<string>();
        var first = new RecordingSceneService("first", calls);
        var second = new RecordingSceneService("second", calls);
        using var services = new SceneServices(document, new PreviewState(), first, second);

        Assert.Same(document, first.Document);
        Assert.Same(document, second.Document);
        MeshObject meshObject = document.Add("Object", Transform3D.Identity, null!);
        document.SetTransform(
            meshObject.Id,
            new Transform3D(Basis.Identity, new Vector3(1, 2, 3))
        );
        document.MarkGeometryChanged(meshObject.Id);
        services.Flush();
        document.Remove(meshObject.Id);
        services.Dispose();

        Assert.Equal(
            [
                "first:set-document",
                "second:set-document",
                $"first:add:{meshObject.Id.Value}",
                $"second:add:{meshObject.Id.Value}",
                $"first:transform:{meshObject.Id.Value}",
                $"second:transform:{meshObject.Id.Value}",
                $"first:geometry:{meshObject.Id.Value}",
                $"second:geometry:{meshObject.Id.Value}",
                "first:flush",
                "second:flush",
                $"first:remove:{meshObject.Id.Value}",
                $"second:remove:{meshObject.Id.Value}",
                "first:dispose",
                "second:dispose",
            ],
            calls
        );
    }

    [Fact]
    public void DisposeUnsubscribesFromTheDocument()
    {
        using var document = new Document();
        var calls = new List<string>();
        var service = new RecordingSceneService("service", calls);
        var services = new SceneServices(document, new PreviewState(), service);
        services.Dispose();
        calls.Clear();

        MeshObject meshObject = document.Add("Object", Transform3D.Identity, null!);
        document.SetTransform(meshObject.Id, Transform3D.Identity);
        document.MarkGeometryChanged(meshObject.Id);
        document.Remove(meshObject.Id);

        Assert.Empty(calls);
    }

    [Fact]
    public void ConstructionReplaysExistingObjectsInRegistrationOrder()
    {
        using var document = new Document();
        MeshObject firstObject = document.Add("First", Transform3D.Identity, null!);
        MeshObject secondObject = document.Add("Second", Transform3D.Identity, null!);
        var calls = new List<string>();
        var firstService = new RecordingSceneService("first", calls);
        var secondService = new RecordingSceneService("second", calls);
        using var services = new SceneServices(
            document,
            new PreviewState(),
            firstService,
            secondService
        );

        Assert.Equal(
            [
                "first:set-document",
                "second:set-document",
                $"first:add:{firstObject.Id.Value}",
                $"second:add:{firstObject.Id.Value}",
                $"first:add:{secondObject.Id.Value}",
                $"second:add:{secondObject.Id.Value}",
            ],
            calls
        );
    }

    [Fact]
    public void SetDocumentUnsubscribesFromTheOldDocumentAndReplaysTheNewDocument()
    {
        using var oldDocument = new Document();
        using var newDocument = new Document();
        MeshObject existingObject = newDocument.Add("Existing", Transform3D.Identity, null!);
        var calls = new List<string>();
        var service = new RecordingSceneService("service", calls);
        using var services = new SceneServices(oldDocument, new PreviewState(), service);

        calls.Clear();
        services.SetDocument(newDocument);
        oldDocument.Add("Old", Transform3D.Identity, null!);
        MeshObject addedObject = newDocument.Add("New", Transform3D.Identity, null!);

        Assert.Equal(
            [
                "service:set-document",
                $"service:add:{existingObject.Id.Value}",
                $"service:add:{addedObject.Id.Value}",
            ],
            calls
        );
        Assert.Same(newDocument, service.Document);
        Assert.Same(newDocument, services.CurrentDocument);
    }

    [Fact]
    public void SetDocumentFailureRaisesOneFatalErrorWithoutEscaping()
    {
        using var oldDocument = new Document();
        using var newDocument = new Document();
        var calls = new List<string>();
        var first = new RecordingSceneService("first", calls);
        var failing = new RecordingSceneService("failing", calls);
        var services = new SceneServices(oldDocument, new PreviewState(), first, failing);
        calls.Clear();
        failing.ThrowOnSetDocument = true;
        var fatalErrors = new List<(string Operation, Exception Exception)>();
        services.FatalError += (operation, exception) => fatalErrors.Add((operation, exception));

        services.SetDocument(newDocument);

        Assert.True(services.IsFaulted);
        Assert.Single(fatalErrors);
        Assert.Equal("replacing the document", fatalErrors[0].Operation);
        Assert.Equal("SetDocument failed.", fatalErrors[0].Exception.Message);
        Assert.Equal(
            [
                "first:set-document",
                "failing:set-document",
            ],
            calls
        );

        calls.Clear();
        oldDocument.Add("Old", Transform3D.Identity, null!);
        newDocument.Add("New", Transform3D.Identity, null!);
        services.SetDocument(oldDocument);
        services.Flush();
        Assert.Empty(calls);
        Assert.Single(fatalErrors);

        services.Dispose();
        Assert.Equal(["first:dispose", "failing:dispose"], calls);
    }

    [Fact]
    public void DocumentEventFailureDoesNotEscapeTheCommandOrPreventItEnteringHistory()
    {
        using var document = new Document();
        MeshObject target = document.Add("Object", Transform3D.Identity, null!);
        var calls = new List<string>();
        var failing = new RecordingSceneService("failing", calls);
        var skipped = new RecordingSceneService("skipped", calls);
        using var services = new SceneServices(document, new PreviewState(), failing, skipped);
        using var history = new CommandHistory();
        history.SetDocument(document);
        calls.Clear();
        failing.ThrowOnTransform = true;
        var fatalErrors = new List<(string Operation, Exception Exception)>();
        services.FatalError += (operation, exception) => fatalErrors.Add((operation, exception));
        var after = new Transform3D(Basis.Identity, new Vector3(4, 5, 6));

        history.Submit(new TranslateCommand(target.Id, Transform3D.Identity, after));

        Assert.True(history.CanUndo);
        Assert.True(document.TryGetMeshTransform(target.Id, out Transform3D actual));
        Assert.Equal(after, actual);
        Assert.True(services.IsFaulted);
        Assert.Single(fatalErrors);
        Assert.Equal("updating a document transform", fatalErrors[0].Operation);
        Assert.Equal("Transform failed.", fatalErrors[0].Exception.Message);
        Assert.Equal(["failing:transform:0"], calls);

        calls.Clear();
        document.MarkGeometryChanged(target.Id);
        services.Flush();
        Assert.Empty(calls);
        Assert.Single(fatalErrors);
    }

    [Fact]
    public void AThrowingFatalErrorSubscriberIsAllowedToPropagate()
    {
        using var document = new Document();
        MeshObject target = document.Add("Object", Transform3D.Identity, null!);
        var service = new RecordingSceneService("failing", []);
        using var services = new SceneServices(document, new PreviewState(), service);
        service.ThrowOnTransform = true;
        int notifiedCount = 0;
        services.FatalError += (_, _) => throw new InvalidOperationException("UI failed.");
        services.FatalError += (_, _) => notifiedCount++;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () =>
                document.SetTransform(
                    target.Id,
                    new Transform3D(Basis.Identity, new Vector3(1, 2, 3))
                )
        );

        Assert.Equal("UI failed.", exception.Message);
        Assert.Equal(0, notifiedCount);
        Assert.True(services.IsFaulted);
    }

    private sealed class RecordingSceneService(
        string name,
        List<string> calls
    ) : ISceneService
    {
        public Document? Document { get; private set; }

        public bool ThrowOnSetDocument { get; set; }

        public bool ThrowOnTransform { get; set; }

        public void SetDocument(Document document)
        {
            calls.Add($"{name}:set-document");
            if (ThrowOnSetDocument)
                throw new InvalidOperationException("SetDocument failed.");

            Document = document;
        }

        public void Dispose()
        {
            calls.Add($"{name}:dispose");
            Document = null;
        }

        public void OnObjectAdded(MeshObject meshObject) =>
            calls.Add($"{name}:add:{meshObject.Id.Value}");

        public void OnObjectRemoved(MeshObject meshObject) =>
            calls.Add($"{name}:remove:{meshObject.Id.Value}");

        public void OnObjectGeometryChanged(MeshObjectId id) =>
            calls.Add($"{name}:geometry:{id.Value}");

        public void OnObjectTransformChanged(MeshObjectId id)
        {
            calls.Add($"{name}:transform:{id.Value}");
            if (ThrowOnTransform)
                throw new InvalidOperationException("Transform failed.");
        }

        public void Flush() => calls.Add($"{name}:flush");
    }
}
