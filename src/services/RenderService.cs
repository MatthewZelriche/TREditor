using System;
using System.Collections.Generic;
using Godot;

public sealed class RenderService : ISceneService
{
    private readonly Node3D _renderRoot;
    private readonly Dictionary<MeshObjectId, RenderEntry> _entries = [];
    private readonly HashSet<MeshObjectId> _dirtyMeshes = [];
    private Scene _scene;
    private bool _disposed;

    public RenderService(Scene scene, Node3D mapRoot)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(mapRoot);

        _scene = scene;
        _renderRoot = new Node3D { Name = "Render" };
        try
        {
            mapRoot.AddChild(_renderRoot);
        }
        catch
        {
            _renderRoot.Free();
            throw;
        }
    }

    public void SetScene(Scene scene)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(scene);
        if (ReferenceEquals(_scene, scene))
            return;

        ClearSceneState();
        _scene = scene;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        ClearSceneState();
        _renderRoot.Free();
    }

    public void OnObjectAdded(Scene.MeshObject meshObject)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(meshObject);
        ArgumentNullException.ThrowIfNull(meshObject.Mesh);

        var entry = new RenderEntry(meshObject, _renderRoot);
        try
        {
            _entries.Add(meshObject.Id, entry);
        }
        catch
        {
            entry.Dispose();
            throw;
        }

        _dirtyMeshes.Add(meshObject.Id);
    }

    public void OnObjectRemoved(Scene.MeshObject meshObject)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(meshObject);

        _dirtyMeshes.Remove(meshObject.Id);
        if (!_entries.Remove(meshObject.Id, out RenderEntry? entry))
            return;

        entry.Dispose();
    }

    public void OnObjectGeometryChanged(MeshObjectId id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_scene.Objects.TryGetValue(id, out Scene.MeshObject? meshObject))
            return;

        meshObject.Mesh.MarkDirty();
        _dirtyMeshes.Add(id);
    }

    public void OnObjectTransformChanged(MeshObjectId id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (
            _scene.Objects.TryGetValue(id, out Scene.MeshObject? meshObject)
            && _entries.TryGetValue(id, out RenderEntry? entry)
        )
        {
            entry.Instance.Transform = meshObject.Transform;
        }
    }

    public void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        foreach (MeshObjectId id in _dirtyMeshes)
        {
            if (
                _scene.Objects.TryGetValue(id, out Scene.MeshObject? meshObject)
                && _entries.TryGetValue(id, out RenderEntry? entry)
            )
            {
                meshObject.Mesh.RebuildSurfaceIfDirty();
                UpdateArrayMesh(entry.ArrayMesh, meshObject.Mesh.Surface);
            }
        }

        _dirtyMeshes.Clear();
    }

    private void ClearSceneState()
    {
        foreach (RenderEntry entry in _entries.Values)
            entry.Dispose();

        _entries.Clear();
        _dirtyMeshes.Clear();
    }

    private static void UpdateArrayMesh(ArrayMesh arrayMesh, MeshSurface surface)
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

        public RenderEntry(Scene.MeshObject meshObject, Node3D renderRoot)
        {
            ArrayMesh = new ArrayMesh();
            Instance = new MeshInstance3D
            {
                Name = $"MeshObject_{meshObject.Id.Value}",
                Mesh = ArrayMesh,
                Transform = meshObject.Transform,
            };

            try
            {
                renderRoot.AddChild(Instance);
            }
            catch
            {
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
            Instance.Mesh = null;
            Instance.Free();
            ArrayMesh.Dispose();
        }
    }
}
