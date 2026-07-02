using TREditorSharp;

namespace TREditor2026.Tests;

public sealed class DocumentServiceTests
{
    [Fact]
    public void Open_InvalidInputLeavesSessionUntouched()
    {
        FakeDocumentSession session = new();
        DocumentService service = new(session, new EditorDocumentSerializer());
        using MemoryStream source = new([1, 2, 3, 4]);

        Assert.Throws<FormatException>(() => service.Open(source));

        Assert.Empty(session.Events);
    }

    [Fact]
    public void Open_ValidInputCancelsPreviewBeforeResetAndApply()
    {
        FakeDocumentSession session = new();
        DocumentService service = new(session, new EditorDocumentSerializer());
        using MemoryStream source = WriteDocument(new EditorDocument([], []));

        service.Open(source);

        Assert.Equal(["Reset", "Apply"], session.Events);
    }

    [Fact]
    public void Open_ApplyFailureResetsPartialReplacement()
    {
        FakeDocumentSession session = new() { ThrowOnApply = true };
        DocumentService service = new(session, new EditorDocumentSerializer());
        using MemoryStream source = WriteDocument(new EditorDocument([], []));

        Assert.Throws<InvalidOperationException>(() => service.Open(source));

        Assert.Equal(["Reset", "Apply", "Reset"], session.Events);
    }

    [Fact]
    public void New_CancelsPreviewBeforeReset()
    {
        FakeDocumentSession session = new();
        DocumentService service = new(session, new EditorDocumentSerializer());

        service.New();

        Assert.Equal(["Reset"], session.Events);
    }

    [Fact]
    public void Save_CancelsPreviewBeforeCapturingCommittedState()
    {
        FakeDocumentSession session = new();
        DocumentService service = new(session, new EditorDocumentSerializer());
        using MemoryStream destination = new();

        service.Save(destination);

        Assert.Equal(["CancelPreview", "Capture"], session.Events);
        Assert.True(destination.Length > 0);
    }

    [Fact]
    public void Save_ReplacesExistingFileWithoutLeavingTemporarySibling()
    {
        const string path = "scene.tred";
        FakeDocumentFileSystem fileSystem = new();
        fileSystem.Files[path] = [1, 2, 3];
        FakeDocumentSession session = new();
        DocumentService service = new(session, new EditorDocumentSerializer(), fileSystem);

        service.Save(path);

        Assert.Equal("TRED"u8.ToArray(), fileSystem.Files[path][..4]);
        Assert.Equal(1, fileSystem.ReplaceCount);
        Assert.DoesNotContain(fileSystem.Files.Keys, candidate => candidate.EndsWith(".tmp"));
    }

    [Fact]
    public void Save_SerializationFailureLeavesExistingFileUntouched()
    {
        const string path = "scene.tred";
        byte[] original = [1, 2, 3];
        FakeDocumentFileSystem fileSystem = new();
        fileSystem.Files[path] = original;
        FakeDocumentSession session = new()
        {
            CaptureResult = new EditorDocument(
                [],
                [new MaterialSlotMapping(1, "not//normalized.png")]
            ),
        };
        DocumentService service = new(session, new EditorDocumentSerializer(), fileSystem);

        Assert.Throws<FormatException>(() => service.Save(path));

        Assert.Same(original, fileSystem.Files[path]);
        Assert.Equal(0, fileSystem.ReplaceCount);
        Assert.DoesNotContain(fileSystem.Files.Keys, candidate => candidate.EndsWith(".tmp"));
    }

    private static MemoryStream WriteDocument(EditorDocument document)
    {
        MemoryStream stream = new();
        new EditorDocumentSerializer().Write(document, stream);
        stream.Position = 0;
        return stream;
    }

    private sealed class FakeDocumentSession : IEditorDocumentSession
    {
        public List<string> Events { get; } = [];
        public bool ThrowOnApply { get; init; }
        public EditorDocument CaptureResult { get; init; } = new([], []);

        public void CancelPreview() => Events.Add("CancelPreview");

        public EditorDocument CaptureDocument()
        {
            Events.Add("Capture");
            return CaptureResult;
        }

        public void Reset() => Events.Add("Reset");

        public void Apply(LoadedEditorDocument document)
        {
            Events.Add("Apply");
            if (ThrowOnApply)
                throw new InvalidOperationException("Expected apply failure.");
        }
    }

    private sealed class FakeDocumentFileSystem : IDocumentFileSystem
    {
        public Dictionary<string, byte[]> Files { get; } = [];
        public int ReplaceCount { get; private set; }

        public byte[] ReadAllBytes(string path) => Files[path];

        public void WriteAllBytes(string path, byte[] data) => Files[path] = data;

        public void Replace(string sourcePath, string destinationPath)
        {
            Files[destinationPath] = Files[sourcePath];
            Files.Remove(sourcePath);
            ReplaceCount++;
        }

        public bool Exists(string path) => Files.ContainsKey(path);

        public void Remove(string path) => Files.Remove(path);
    }
}
