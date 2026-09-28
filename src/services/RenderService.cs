using System;
using System.Collections.Generic;
using Godot;

public sealed class RenderService : ISceneService
{
    private readonly Node3D _renderRoot;
    private readonly Dictionary<MeshObjectId, RenderEntry> _entries = [];
    private readonly HashSet<MeshObjectId> _dirtyMeshes = [];
    private readonly PreviewState _previews;
    // TODO: Consider moving this to PreviewState. Maybe RenderService should only be a "dumb renderer"
    // that takes in renderdata and renders it.
    private readonly PreviewGeometryRenderer _previewGeometryRenderer;
    private readonly DefaultMeshMaterial _defaultMaterial;
    private readonly MeshStencilReferences _stencilReferences;
    private Document _document;
    private bool _disposed;

    public RenderService(Document document, Node3D mapRoot, PreviewState previews)
        : this(document, mapRoot, previews, new MeshStencilReferences()) { }

    internal RenderService(
        Document document,
        Node3D mapRoot,
        PreviewState previews,
        MeshStencilReferences stencilReferences
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(mapRoot);
        ArgumentNullException.ThrowIfNull(previews);
        ArgumentNullException.ThrowIfNull(stencilReferences);

        _document = document;
        _previews = previews;
        _stencilReferences = stencilReferences;
        _renderRoot = new Node3D { Name = "Render" };
        PreviewGeometryRenderer? previewGeometryRenderer = null;
        DefaultMeshMaterial? defaultMaterial = null;
        try
        {
            mapRoot.AddChild(_renderRoot);
            previewGeometryRenderer = new PreviewGeometryRenderer(_renderRoot);
            defaultMaterial = new DefaultMeshMaterial();
        }
        catch
        {
            defaultMaterial?.Dispose();
            previewGeometryRenderer?.Dispose();
            _renderRoot.Free();
            throw;
        }

        _previewGeometryRenderer = previewGeometryRenderer;
        _defaultMaterial = defaultMaterial;
    }

    public void SetDocument(Document document)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(document);
        if (ReferenceEquals(_document, document))
            return;

        ClearDocumentState();
        _previewGeometryRenderer.Clear();
        _document = document;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        ClearDocumentState();
        _previewGeometryRenderer.Dispose();
        _defaultMaterial.Dispose();
        _renderRoot.Free();
    }

    public void OnDocumentChanged(DocumentChangeSet changeSet)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(changeSet);

        DocumentObjectChange change = changeSet.Change;
        switch (change.Kind)
        {
            case DocumentChangeKind.Added:
                AddObject(change.ObjectId);
                break;
            case DocumentChangeKind.Removed:
                RemoveObject(change.ObjectId);
                break;
            case DocumentChangeKind.TransformChanged:
                ApplyTransform(change.ObjectId);
                break;
            case DocumentChangeKind.GeometryChanged:
                MarkGeometryDirty(change.ObjectId);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(changeSet),
                    change.Kind,
                    "Unknown document change kind."
                );
        }
    }

    public void OnPreviewTransformChanged(MeshObjectId id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ApplyTransform(id);
    }

    public void OnPreviewGeometryChanged()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _previewGeometryRenderer.Update(_previews.Geometry);
    }

    private void AddObject(MeshObjectId id)
    {
        if (!_document.TryGetObject(id, out MeshObject meshObject))
            throw new InvalidOperationException($"Document object {id.Value} was added but is missing.");

        ArgumentNullException.ThrowIfNull(meshObject.Mesh);

        int stencilReference = _stencilReferences.Acquire(id);
        var entry = new RenderEntry(
            meshObject,
            _renderRoot,
            EffectiveTransform(meshObject),
            _defaultMaterial.GetMaterial(stencilReference)
        );
        try
        {
            _entries.Add(meshObject.Id, entry);
        }
        catch
        {
            entry.Dispose();
            _stencilReferences.Release(id);
            throw;
        }

        _dirtyMeshes.Add(meshObject.Id);
    }

    private void RemoveObject(MeshObjectId id)
    {
        _dirtyMeshes.Remove(id);
        if (!_entries.Remove(id, out RenderEntry? entry))
            return;

        entry.Dispose();
        _stencilReferences.Release(id);
    }

    private void MarkGeometryDirty(MeshObjectId id)
    {
        if (!_document.TryGetObject(id, out MeshObject meshObject))
            return;

        meshObject.Mesh.MarkDirty();
        _dirtyMeshes.Add(id);
    }

    private void ApplyTransform(MeshObjectId id)
    {
        if (
            _document.TryGetObject(id, out MeshObject meshObject)
            && _entries.TryGetValue(id, out RenderEntry? entry)
        )
        {
            entry.Instance.Transform = EffectiveTransform(meshObject);
        }
    }

    // An active preview wins over the committed transform, so a command that lands mid-drag
    // updates the document without disturbing what the tool is showing.
    private Transform3D EffectiveTransform(MeshObject meshObject) =>
        _previews.TryGetTransform(meshObject.Id, out Transform3D preview)
            ? preview
            : meshObject.Transform;

    public void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        foreach (MeshObjectId id in _dirtyMeshes)
        {
            if (
                _document.TryGetObject(id, out MeshObject meshObject)
                && _entries.TryGetValue(id, out RenderEntry? entry)
            )
            {
                meshObject.Mesh.RebuildSurfaceIfDirty();
                UpdateArrayMesh(entry.ArrayMesh, meshObject.Mesh.Surface);
            }
        }

        _dirtyMeshes.Clear();
    }

    private void ClearDocumentState()
    {
        foreach (RenderEntry entry in _entries.Values)
            entry.Dispose();

        _entries.Clear();
        _dirtyMeshes.Clear();
        _stencilReferences.Clear();
    }

    internal static void UpdateArrayMesh(ArrayMesh arrayMesh, MeshSurface surface)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = ConvertVertices(surface.Vertices);
        arrays[(int)Mesh.ArrayType.Normal] = ConvertVertices(surface.Normals);
        arrays[(int)Mesh.ArrayType.TexUV] = ConvertUvs(surface.Uvs);
        arrays[(int)Mesh.ArrayType.Index] = surface.ElementIndices.ToArray();

        arrayMesh.ClearSurfaces();
        if (surface.ElementIndices.Count != 0)
            arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
    }

    private static Vector3[] ConvertVertices(List<System.Numerics.Vector3> source)
    {
        var result = new Vector3[source.Count];
        for (int index = 0; index < source.Count; index++)
        {
            System.Numerics.Vector3 value = source[index];
            result[index] = new Vector3(value.X, value.Y, value.Z);
        }

        return result;
    }

    private static Vector2[] ConvertUvs(List<System.Numerics.Vector2> source)
    {
        var result = new Vector2[source.Count];
        for (int index = 0; index < source.Count; index++)
        {
            System.Numerics.Vector2 value = source[index];
            result[index] = new Vector2(value.X, value.Y);
        }

        return result;
    }

    private sealed class RenderEntry : IDisposable
    {
        private bool _disposed;

        public RenderEntry(
            MeshObject meshObject,
            Node3D renderRoot,
            Transform3D transform,
            Material material
        )
        {
            ArrayMesh = new ArrayMesh();
            Instance = new MeshInstance3D
            {
                Name = $"MeshObject_{meshObject.Id.Value}",
                Mesh = ArrayMesh,
                Transform = transform,
                MaterialOverride = material,
            };

            try
            {
                renderRoot.AddChild(Instance);
            }
            catch
            {
                Instance.MaterialOverride = null;
                Instance.Mesh = null;
                Instance.Free();
                ArrayMesh.Dispose();
                throw;
            }
        }

        public ArrayMesh ArrayMesh { get; }

        public MeshInstance3D Instance { get; }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Instance.MaterialOverride = null;
            Instance.Mesh = null;
            Instance.Free();
            ArrayMesh.Dispose();
        }
    }
}
