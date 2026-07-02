using System;
using System.IO;
using Godot;
using FileAccess = Godot.FileAccess;

internal interface IDocumentFileSystem
{
    byte[] ReadAllBytes(string path);

    void WriteAllBytes(string path, byte[] data);

    void Replace(string sourcePath, string destinationPath);

    bool Exists(string path);

    void Remove(string path);
}

/// <summary>
/// Keeps Godot's native path-aware file APIs at the edge of the otherwise testable document
/// transaction. In particular, this preserves support for res:// and user:// paths.
/// </summary>
internal sealed class GodotDocumentFileSystem : IDocumentFileSystem
{
    public byte[] ReadAllBytes(string path)
    {
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
            throw new IOException(
                $"Unable to open document '{path}': {FileAccess.GetOpenError()}."
            );

        ulong length = file.GetLength();
        if (length > int.MaxValue)
            throw new IOException($"Document '{path}' is too large to load.");
        return file.GetBuffer((long)length);
    }

    public void WriteAllBytes(string path, byte[] data)
    {
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
            throw new IOException(
                $"Unable to save temporary document '{path}': {FileAccess.GetOpenError()}."
            );

        file.StoreBuffer(data);
        file.Flush();
    }

    public void Replace(string sourcePath, string destinationPath)
    {
        Error error = DirAccess.RenameAbsolute(sourcePath, destinationPath);
        if (error != Error.Ok)
            throw new IOException(
                $"Unable to replace document '{destinationPath}' with its temporary file: {error}."
            );
    }

    public bool Exists(string path) => FileAccess.FileExists(path);

    public void Remove(string path) => DirAccess.RemoveAbsolute(path);
}
