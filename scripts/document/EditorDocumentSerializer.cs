using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;
using TREditorSharp;
using TREditorSharp.IO;

/// <summary>
/// Reads and writes the self-contained <c>.tred</c> document container. The container stores
/// editor-level metadata (object identity, name, transform, and the material-slot table) and embeds
/// each object's geometry as a TRMesh binary (<c>.trmb</c>) blob, reusing the already-tested mesh
/// I/O for the heavy topology/column work.
/// </summary>
public sealed class EditorDocumentSerializer
{
    public const string FileExtension = "tred";

    private static readonly byte[] Magic = [(byte)'T', (byte)'R', (byte)'E', (byte)'D'];
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private const int Version = 1;

    // Checked before allocating; generous for real documents, not int.MaxValue.
    private const int MaximumMaterialMappings = 1_000_000;
    private const int MaximumObjects = 1_000_000;

    // Per string field (object name, texture asset ID); UTF-8 byte length, max 1 MiB.
    private const int MaximumStringBytes = 1_048_576;

    // Per object embedded TRMesh blob; max 512 MiB (matches TRMesh column payload cap).
    private const int MaximumEmbeddedMeshBytes = 536_870_912;

    // Wire-format lower bounds for truncation checks before reading each entry.
    private const int MinimumMappingBytes = sizeof(int) + 1;
    private const int MinimumObjectBytes = 16 + 1 + 12 * sizeof(float) + sizeof(long);

    private readonly BinaryMeshWriter _meshWriter = new();
    private readonly BinaryMeshReader _meshReader = new();
    private readonly Func<Stream, SpatialMesh> _readMesh;

    public EditorDocumentSerializer()
    {
        _readMesh = source => _meshReader.ReadSpatialMesh(source);
    }

    internal EditorDocumentSerializer(Func<Stream, SpatialMesh> readMesh)
    {
        ArgumentNullException.ThrowIfNull(readMesh);
        _readMesh = readMesh;
    }

    public void Write(EditorDocument document, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);
        EditorDocumentValidator.Validate(document);
        ValidateCountForWrite(
            document.MaterialMappings.Count,
            MaximumMaterialMappings,
            "material mapping"
        );
        ValidateCountForWrite(document.Objects.Count, MaximumObjects, "object");

        using var writer = new BinaryWriter(destination, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(Version);

        writer.Write(document.MaterialMappings.Count);
        foreach (MaterialSlotMapping mapping in document.MaterialMappings)
        {
            writer.Write(mapping.Slot);
            WriteString(writer, mapping.AssetId, "texture asset ID");
        }

        writer.Write(document.Objects.Count);
        foreach (EditorDocumentObject documentObject in document.Objects)
        {
            writer.Write(documentObject.Id.Value.ToByteArray());
            WriteString(writer, documentObject.Name, "object name");
            WriteTransform(writer, documentObject.Transform);
            WriteMeshBlob(writer, documentObject.Mesh);
        }
    }

    public LoadedEditorDocument Read(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        using var reader = new BinaryReader(source, Encoding.UTF8, leaveOpen: true);
        List<EditorDocumentObject> objects = [];
        try
        {
            int version = ReadHeader(reader);
            EditorDocument document = version switch
            {
                1 => ReadVersion1(reader, objects),
                _ => throw new NotSupportedException(
                    $"Unsupported .tred document version {version}; expected {Version}."
                ),
            };

            EnsureAtEnd(source);
            EditorDocumentValidator.Validate(document);
            return new LoadedEditorDocument(document);
        }
        catch
        {
            foreach (EditorDocumentObject documentObject in objects)
                documentObject.Mesh.Dispose();
            throw;
        }
    }

    private EditorDocument ReadVersion1(BinaryReader reader, List<EditorDocumentObject> objects)
    {
        int mappingCount = ReadCount(
            reader,
            "material mapping",
            MaximumMaterialMappings,
            MinimumMappingBytes
        );
        var mappings = new List<MaterialSlotMapping>(mappingCount);
        for (int i = 0; i < mappingCount; i++)
        {
            int slot = reader.ReadInt32();
            string assetId = ReadString(reader, "texture asset ID");
            mappings.Add(new MaterialSlotMapping(slot, assetId));
        }

        int objectCount = ReadCount(reader, "object", MaximumObjects, MinimumObjectBytes);
        objects.Capacity = objectCount;
        for (int i = 0; i < objectCount; i++)
        {
            var id = new EditorObjectId(ReadGuid(reader));
            string name = ReadString(reader, "object name");
            Transform3D transform = ReadTransform(reader);
            SpatialMesh mesh = ReadMeshBlob(reader);
            objects.Add(new EditorDocumentObject(id, name, transform, mesh));
        }

        return new EditorDocument(objects, mappings);
    }

    private void WriteMeshBlob(BinaryWriter writer, SpatialMesh mesh)
    {
        using var meshStream = new MemoryStream();
        _meshWriter.Write(mesh, meshStream);
        byte[] bytes = meshStream.ToArray();
        if (bytes.Length > MaximumEmbeddedMeshBytes)
            throw new InvalidOperationException(
                $"Embedded mesh blob length {bytes.Length} exceeds the supported maximum of "
                    + $"{MaximumEmbeddedMeshBytes}."
            );
        writer.Write((long)bytes.Length);
        writer.Write(bytes);
    }

    private SpatialMesh ReadMeshBlob(BinaryReader reader)
    {
        long length = reader.ReadInt64();
        if (length < 0 || length > MaximumEmbeddedMeshBytes)
            throw new FormatException($"Embedded mesh blob length {length} is invalid.");
        EnsureRemaining(reader.BaseStream, length, "embedded mesh blob");

        byte[] bytes = reader.ReadBytes((int)length);
        if (bytes.Length != length)
            throw new EndOfStreamException("Embedded mesh blob is truncated.");

        using var meshStream = new MemoryStream(bytes, writable: false);
        return _readMesh(meshStream);
    }

    private static int ReadHeader(BinaryReader reader)
    {
        byte[] magic = reader.ReadBytes(Magic.Length);
        if (magic.Length != Magic.Length || !magic.AsSpan().SequenceEqual(Magic))
            throw new FormatException("Stream is not a TREditor document (.tred) file.");

        return reader.ReadInt32();
    }

    private static int ReadCount(
        BinaryReader reader,
        string label,
        int maximum,
        int minimumBytesPerItem
    )
    {
        int count = reader.ReadInt32();
        if (count < 0)
            throw new FormatException($"Negative {label} count {count} in .tred document.");
        if (count > maximum)
            throw new FormatException(
                $"{label} count {count} exceeds the supported maximum of {maximum}."
            );

        long minimumBytes = checked((long)count * minimumBytesPerItem);
        EnsureRemaining(reader.BaseStream, minimumBytes, $"{label} entries");
        return count;
    }

    private static string ReadString(BinaryReader reader, string label)
    {
        int byteCount = reader.Read7BitEncodedInt();
        if (byteCount < 0 || byteCount > MaximumStringBytes)
            throw new FormatException($"{label} byte length {byteCount} is invalid.");
        EnsureRemaining(reader.BaseStream, byteCount, label);

        byte[] bytes = reader.ReadBytes(byteCount);
        if (bytes.Length != byteCount)
            throw new EndOfStreamException($"{label} is truncated.");
        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new FormatException($"{label} is not valid UTF-8.", exception);
        }
    }

    private static void WriteString(BinaryWriter writer, string value, string label)
    {
        byte[] bytes;
        try
        {
            bytes = StrictUtf8.GetBytes(value);
        }
        catch (EncoderFallbackException exception)
        {
            throw new InvalidOperationException($"{label} is not valid UTF-8.", exception);
        }

        if (bytes.Length > MaximumStringBytes)
            throw new InvalidOperationException(
                $"{label} byte length {bytes.Length} exceeds the supported maximum of "
                    + $"{MaximumStringBytes}."
            );

        writer.Write7BitEncodedInt(bytes.Length);
        writer.Write(bytes);
    }

    private static void EnsureRemaining(Stream source, long requiredBytes, string label)
    {
        if (requiredBytes < 0)
            throw new FormatException($"{label} has a negative byte length.");
        if (source.CanSeek && requiredBytes > source.Length - source.Position)
            throw new EndOfStreamException($"{label} is truncated.");
    }

    private static void EnsureAtEnd(Stream source)
    {
        if (source.CanSeek && source.Position != source.Length)
            throw new FormatException("Unexpected trailing data after .tred document.");
    }

    private static void ValidateCountForWrite(int count, int maximum, string label)
    {
        if (count > maximum)
            throw new InvalidOperationException(
                $"Cannot write {count} {label} entries; maximum is {maximum}."
            );
    }

    private static Guid ReadGuid(BinaryReader reader)
    {
        byte[] bytes = reader.ReadBytes(16);
        if (bytes.Length != 16)
            throw new EndOfStreamException("Object identity is truncated.");
        return new Guid(bytes);
    }

    private static void WriteTransform(BinaryWriter writer, Transform3D transform)
    {
        WriteVector3(writer, transform.Basis.Column0);
        WriteVector3(writer, transform.Basis.Column1);
        WriteVector3(writer, transform.Basis.Column2);
        WriteVector3(writer, transform.Origin);
    }

    private static Transform3D ReadTransform(BinaryReader reader)
    {
        Vector3 column0 = ReadVector3(reader);
        Vector3 column1 = ReadVector3(reader);
        Vector3 column2 = ReadVector3(reader);
        Vector3 origin = ReadVector3(reader);
        return new Transform3D(new Basis(column0, column1, column2), origin);
    }

    private static void WriteVector3(BinaryWriter writer, Vector3 value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
    }

    private static Vector3 ReadVector3(BinaryReader reader) =>
        new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
}
