using Godot;
using TREditorSharp;
using TREditorSharp.Builders;
using NumericsVector2 = System.Numerics.Vector2;
using NumericsVector3 = System.Numerics.Vector3;

namespace TREditor2026.Tests;

public sealed class EditorDocumentSerializerTests
{
    [Fact]
    public void RoundTrip_PreservesObjectMetadataMaterialTableAndGeometry()
    {
        using SpatialMesh mesh = MeshBuilders.Build(
            new BlockOptions
            {
                Min = new NumericsVector3(-1f, -2f, -3f),
                Max = new NumericsVector3(1f, 2f, 3f),
            }
        );

        const int slot = 7;
        var expectedUv = new NumericsVector2(0.25f, 0.75f);
        FaceHandle texturedFace = FirstFace(mesh);
        mesh.SetFaceMaterialSlot(texturedFace, slot);
        mesh.SetFaceUvsInitialized(texturedFace, true);
        foreach (HalfEdgeHandle corner in mesh.HalfEdgesAroundFace(texturedFace))
        {
            mesh.SetFaceCornerUv(corner, expectedUv);
        }

        var id = new EditorObjectId(Guid.Parse("11111111-2222-3333-4444-555555555555"));
        var transform = new Transform3D(
            new Basis(Vector3.Up, 0.5f).Scaled(new Vector3(1f, 2f, 3f)),
            new Vector3(10f, 20f, 30f)
        );
        MaterialSlotMapping[] mappings = [new MaterialSlotMapping(slot, "walls/brick.png")];
        var document = new EditorDocument(
            [new EditorDocumentObject(id, "Box", transform, mesh)],
            mappings
        );

        var serializer = new EditorDocumentSerializer();
        using var stream = new MemoryStream();
        serializer.Write(document, stream);
        stream.Position = 0;
        using LoadedEditorDocument loaded = serializer.Read(stream);
        EditorDocument reloaded = loaded.Document;

        Assert.Single(reloaded.Objects);
        EditorDocumentObject reloadedObject = reloaded.Objects[0];
        Assert.Equal(id, reloadedObject.Id);
        Assert.Equal("Box", reloadedObject.Name);
        AssertTransformsEqual(transform, reloadedObject.Transform);
        Assert.Equal(mappings, reloaded.MaterialMappings.ToArray());

        // Full mesh fidelity is covered by TRMesh's own binary tests; here we spot-check that the
        // embedded blob is intact and that the textured face's slot and UVs survived.
        Assert.Equal(SortedPositions(mesh), SortedPositions(reloadedObject.Mesh));
        FaceHandle reloadedTexturedFace = SingleFaceWithSlot(reloadedObject.Mesh, slot);
        foreach (
            HalfEdgeHandle corner in reloadedObject.Mesh.HalfEdgesAroundFace(reloadedTexturedFace)
        )
        {
            Assert.Equal(expectedUv, reloadedObject.Mesh.GetFaceCornerUv(corner));
        }
    }

    [Fact]
    public void RoundTrip_EmptyDocument()
    {
        var document = new EditorDocument([], []);
        var serializer = new EditorDocumentSerializer();

        using var stream = new MemoryStream();
        serializer.Write(document, stream);
        stream.Position = 0;
        using LoadedEditorDocument loaded = serializer.Read(stream);
        EditorDocument reloaded = loaded.Document;

        Assert.Empty(reloaded.Objects);
        Assert.Empty(reloaded.MaterialMappings);
    }

    [Fact]
    public void Read_RejectsDataThatIsNotATredDocument()
    {
        var serializer = new EditorDocumentSerializer();
        using var stream = new MemoryStream([1, 2, 3, 4, 5, 6, 7, 8]);

        Assert.Throws<FormatException>(() => serializer.Read(stream));
    }

    [Fact]
    public void Read_RejectsUnsupportedVersion()
    {
        var serializer = new EditorDocumentSerializer();
        using MemoryStream stream = WriteRawDocument([], [], version: 2);

        Assert.Throws<NotSupportedException>(() => serializer.Read(stream));
    }

    [Fact]
    public void Read_RejectsExcessiveCountBeforeAllocating()
    {
        using MemoryStream stream = new();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("TRED"u8);
            writer.Write(1);
            writer.Write(int.MaxValue);
        }
        stream.Position = 0;

        Assert.Throws<FormatException>(() => new EditorDocumentSerializer().Read(stream));
    }

    [Fact]
    public void Read_RejectsInvalidEmbeddedBlobLengthBeforeAllocating()
    {
        RawObject rawObject = new(
            Guid.NewGuid(),
            "Box",
            Transform3D.Identity,
            DeclaredBlobLength: 536_870_913,
            Blob: []
        );
        using MemoryStream stream = WriteRawDocument([], [rawObject]);

        Assert.Throws<FormatException>(() => new EditorDocumentSerializer().Read(stream));
    }

    [Fact]
    public void Write_RejectsStringThatReaderWouldNotAccept()
    {
        string oversizedName = new('x', 1_048_577);
        using SpatialMesh mesh = new();
        EditorDocument document = new(
            [
                new EditorDocumentObject(
                    EditorObjectId.New(),
                    oversizedName,
                    Transform3D.Identity,
                    mesh
                ),
            ],
            []
        );
        using MemoryStream stream = new();

        Assert.Throws<InvalidOperationException>(
            () => new EditorDocumentSerializer().Write(document, stream)
        );
    }

    [Fact]
    public void Read_DisposesPreviouslyReadMeshesWhenLaterObjectFails()
    {
        SpatialMesh first = new();
        int readCount = 0;
        var serializer = new EditorDocumentSerializer(_ =>
        {
            if (readCount++ == 0)
                return first;
            throw new FormatException("Second mesh is corrupt.");
        });
        using MemoryStream stream = WriteRawDocument(
            [],
            [
                new RawObject(Guid.NewGuid(), "First", Transform3D.Identity),
                new RawObject(Guid.NewGuid(), "Second", Transform3D.Identity),
            ]
        );

        Assert.Throws<FormatException>(() => serializer.Read(stream));

        Assert.Throws<ObjectDisposedException>(() => first.AddVertex(NumericsVector3.Zero));
    }

    [Fact]
    public void Read_RejectsDuplicateObjectIdsAndDisposesEveryMesh()
    {
        SpatialMesh first = new();
        SpatialMesh second = new();
        Queue<SpatialMesh> meshes = new([first, second]);
        var serializer = new EditorDocumentSerializer(_ => meshes.Dequeue());
        Guid duplicateId = Guid.NewGuid();
        using MemoryStream stream = WriteRawDocument(
            [],
            [
                new RawObject(duplicateId, "First", Transform3D.Identity),
                new RawObject(duplicateId, "Second", Transform3D.Identity),
            ]
        );

        Assert.Throws<FormatException>(() => serializer.Read(stream));

        Assert.Throws<ObjectDisposedException>(() => first.AddVertex(NumericsVector3.Zero));
        Assert.Throws<ObjectDisposedException>(() => second.AddVertex(NumericsVector3.Zero));
    }

    [Fact]
    public void Read_RejectsNonFiniteTransformAndDisposesMesh()
    {
        SpatialMesh mesh = new();
        var serializer = new EditorDocumentSerializer(_ => mesh);
        Transform3D transform = new(Basis.Identity, new Vector3(float.NaN, 0f, 0f));
        using MemoryStream stream = WriteRawDocument(
            [],
            [new RawObject(Guid.NewGuid(), "Invalid", transform)]
        );

        Assert.Throws<FormatException>(() => serializer.Read(stream));

        Assert.Throws<ObjectDisposedException>(() => mesh.AddVertex(NumericsVector3.Zero));
    }

    [Fact]
    public void Read_RejectsTrailingData()
    {
        using MemoryStream stream = WriteRawDocument([], [], trailingBytes: [1]);

        Assert.Throws<FormatException>(() => new EditorDocumentSerializer().Read(stream));
    }

    [Fact]
    public void LoadedDocument_DisposesMeshesThatWereNotTransferred()
    {
        SpatialMesh mesh = new();
        var serializer = new EditorDocumentSerializer(_ => mesh);
        using MemoryStream stream = WriteRawDocument(
            [],
            [new RawObject(Guid.NewGuid(), "Box", Transform3D.Identity)]
        );

        LoadedEditorDocument loaded = serializer.Read(stream);
        loaded.Dispose();

        Assert.Throws<ObjectDisposedException>(() => mesh.AddVertex(NumericsVector3.Zero));
    }

    [Fact]
    public void LoadedDocument_DoesNotDisposeTransferredMesh()
    {
        using SpatialMesh mesh = new();
        var serializer = new EditorDocumentSerializer(_ => mesh);
        using MemoryStream stream = WriteRawDocument(
            [],
            [new RawObject(Guid.NewGuid(), "Box", Transform3D.Identity)]
        );

        LoadedEditorDocument loaded = serializer.Read(stream);
        loaded.TransferMeshOwnership(loaded.Objects[0]);
        loaded.Dispose();

        mesh.AddVertex(NumericsVector3.Zero);
    }

    [Fact]
    public void Validate_RejectsDuplicateMaterialSlotsAndAssets()
    {
        EditorDocument duplicateSlots = new(
            [],
            [new MaterialSlotMapping(1, "a.png"), new MaterialSlotMapping(1, "b.png")]
        );
        EditorDocument duplicateAssets = new(
            [],
            [new MaterialSlotMapping(1, "a.png"), new MaterialSlotMapping(2, "a.png")]
        );

        Assert.Throws<FormatException>(() => EditorDocumentValidator.Validate(duplicateSlots));
        Assert.Throws<FormatException>(() => EditorDocumentValidator.Validate(duplicateAssets));
    }

    [Fact]
    public void Validate_RejectsNonPositiveSlotsAndNonNormalizedAssets()
    {
        EditorDocument invalidSlot = new([], [new MaterialSlotMapping(0, "a.png")]);
        EditorDocument invalidAsset = new([], [new MaterialSlotMapping(1, "a//b.png")]);

        Assert.Throws<FormatException>(() => EditorDocumentValidator.Validate(invalidSlot));
        Assert.Throws<FormatException>(() => EditorDocumentValidator.Validate(invalidAsset));
    }

    [Fact]
    public void Validate_RejectsEmptyObjectId()
    {
        using SpatialMesh mesh = new();
        EditorDocument document = new(
            [
                new EditorDocumentObject(
                    new EditorObjectId(Guid.Empty),
                    "Box",
                    Transform3D.Identity,
                    mesh
                ),
            ],
            []
        );

        Assert.Throws<FormatException>(() => EditorDocumentValidator.Validate(document));
    }

    private static void AssertTransformsEqual(Transform3D expected, Transform3D actual)
    {
        Assert.Equal(expected.Basis.Column0, actual.Basis.Column0);
        Assert.Equal(expected.Basis.Column1, actual.Basis.Column1);
        Assert.Equal(expected.Basis.Column2, actual.Basis.Column2);
        Assert.Equal(expected.Origin, actual.Origin);
    }

    private static MemoryStream WriteRawDocument(
        IReadOnlyList<MaterialSlotMapping> mappings,
        IReadOnlyList<RawObject> objects,
        int version = 1,
        byte[]? trailingBytes = null
    )
    {
        MemoryStream stream = new();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("TRED"u8);
            writer.Write(version);
            writer.Write(mappings.Count);
            foreach (MaterialSlotMapping mapping in mappings)
            {
                writer.Write(mapping.Slot);
                writer.Write(mapping.AssetId);
            }

            writer.Write(objects.Count);
            foreach (RawObject documentObject in objects)
            {
                writer.Write(documentObject.Id.ToByteArray());
                writer.Write(documentObject.Name);
                WriteTransform(writer, documentObject.Transform);
                writer.Write(documentObject.DeclaredBlobLength);
                writer.Write(documentObject.Blob ?? []);
            }

            if (trailingBytes != null)
                writer.Write(trailingBytes);
        }

        stream.Position = 0;
        return stream;
    }

    private static void WriteTransform(BinaryWriter writer, Transform3D transform)
    {
        WriteVector3(writer, transform.Basis.Column0);
        WriteVector3(writer, transform.Basis.Column1);
        WriteVector3(writer, transform.Basis.Column2);
        WriteVector3(writer, transform.Origin);
    }

    private static void WriteVector3(BinaryWriter writer, Vector3 value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
    }

    private static FaceHandle FirstFace(SpatialMesh mesh)
    {
        foreach (FaceHandle face in mesh.EnumerateLiveFaces())
        {
            return face;
        }

        throw new InvalidOperationException("Mesh has no live faces.");
    }

    private static FaceHandle SingleFaceWithSlot(SpatialMesh mesh, int slot)
    {
        FaceHandle match = default;
        int count = 0;
        foreach (FaceHandle face in mesh.EnumerateLiveFaces())
        {
            if (mesh.GetFaceMaterialSlot(face) == slot)
            {
                match = face;
                count++;
            }
        }

        Assert.Equal(1, count);
        return match;
    }

    private static List<NumericsVector3> SortedPositions(SpatialMesh mesh)
    {
        List<NumericsVector3> positions = [];
        foreach (VertexHandle vertex in mesh.EnumerateLiveVertices())
        {
            positions.Add(mesh.GetVertexPosition(vertex));
        }

        positions.Sort(
            (left, right) =>
            {
                int compareX = left.X.CompareTo(right.X);
                if (compareX != 0)
                {
                    return compareX;
                }

                int compareY = left.Y.CompareTo(right.Y);
                return compareY != 0 ? compareY : left.Z.CompareTo(right.Z);
            }
        );
        return positions;
    }

    private sealed record RawObject(
        Guid Id,
        string Name,
        Transform3D Transform,
        long DeclaredBlobLength = 0,
        byte[]? Blob = null
    );
}
