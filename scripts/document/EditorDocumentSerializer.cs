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
    private const int Version = 1;

    private readonly BinaryMeshWriter _meshWriter = new();
    private readonly BinaryMeshReader _meshReader = new();

    public void Write(EditorDocument document, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);

        using var writer = new BinaryWriter(destination, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(Version);

        writer.Write(document.MaterialMappings.Count);
        foreach (MaterialSlotMapping mapping in document.MaterialMappings)
        {
            writer.Write(mapping.Slot);
            writer.Write(mapping.AssetId);
        }

        writer.Write(document.Objects.Count);
        foreach (EditorDocumentObject documentObject in document.Objects)
        {
            writer.Write(documentObject.Id.Value.ToByteArray());
            writer.Write(documentObject.Name);
            WriteTransform(writer, documentObject.Transform);
            WriteMeshBlob(writer, documentObject.Mesh);
        }
    }

    public EditorDocument Read(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        using var reader = new BinaryReader(source, Encoding.UTF8, leaveOpen: true);
        ReadHeader(reader);

        int mappingCount = ReadCount(reader, "material mapping");
        var mappings = new List<MaterialSlotMapping>(mappingCount);
        for (int i = 0; i < mappingCount; i++)
        {
            int slot = reader.ReadInt32();
            string assetId = reader.ReadString();
            mappings.Add(new MaterialSlotMapping(slot, assetId));
        }

        int objectCount = ReadCount(reader, "object");
        var objects = new List<EditorDocumentObject>(objectCount);
        for (int i = 0; i < objectCount; i++)
        {
            var id = new EditorObjectId(ReadGuid(reader));
            string name = reader.ReadString();
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
        writer.Write((long)bytes.Length);
        writer.Write(bytes);
    }

    private SpatialMesh ReadMeshBlob(BinaryReader reader)
    {
        long length = reader.ReadInt64();
        if (length < 0 || length > int.MaxValue)
            throw new FormatException($"Embedded mesh blob length {length} is invalid.");

        byte[] bytes = reader.ReadBytes((int)length);
        if (bytes.Length != length)
            throw new EndOfStreamException("Embedded mesh blob is truncated.");

        using var meshStream = new MemoryStream(bytes, writable: false);
        return _meshReader.ReadSpatialMesh(meshStream);
    }

    private static void ReadHeader(BinaryReader reader)
    {
        byte[] magic = reader.ReadBytes(Magic.Length);
        if (magic.Length != Magic.Length || !magic.AsSpan().SequenceEqual(Magic))
            throw new FormatException("Stream is not a TREditor document (.tred) file.");

        int version = reader.ReadInt32();
        if (version != Version)
            throw new NotSupportedException(
                $"Unsupported .tred document version {version}; expected {Version}."
            );
    }

    private static int ReadCount(BinaryReader reader, string label)
    {
        int count = reader.ReadInt32();
        if (count < 0)
            throw new FormatException($"Negative {label} count {count} in .tred document.");
        return count;
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
