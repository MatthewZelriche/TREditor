using System;
using System.Collections.Generic;
using Godot;

public sealed class PickingService : ISceneService
{
    private const uint PickingCollisionLayer = 1u << 31;
    private const float RayLength = 100_000.0f;

    private readonly Node3D _mapRoot;
    private readonly Node3D _pickingRoot;
    private readonly Dictionary<MeshObjectId, PickingEntry> _entries = [];
    private readonly Dictionary<ulong, MeshObjectId> _colliderIds = [];
    private readonly HashSet<MeshObjectId> _dirtyMeshes = [];
    private readonly PreviewState _previews;
    private Scene _scene;
    private bool _disposed;

    public PickingService(Scene scene, Node3D mapRoot, PreviewState previews)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(mapRoot);
        ArgumentNullException.ThrowIfNull(previews);

        _scene = scene;
        _previews = previews;
        _mapRoot = mapRoot;
        _pickingRoot = new Node3D { Name = "Picking" };
        try
        {
            _mapRoot.AddChild(_pickingRoot);
        }
        catch
        {
            _pickingRoot.Free();
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
        _pickingRoot.Free();
    }

    public void OnObjectAdded(Scene.MeshObject meshObject)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(meshObject);
        ArgumentNullException.ThrowIfNull(meshObject.Mesh);

        var entry = new PickingEntry(meshObject, _pickingRoot, EffectiveTransform(meshObject));
        try
        {
            _entries.Add(meshObject.Id, entry);
            _colliderIds.Add(entry.Body.GetInstanceId(), meshObject.Id);
        }
        catch
        {
            _entries.Remove(meshObject.Id);
            _colliderIds.Remove(entry.Body.GetInstanceId());
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
        if (!_entries.Remove(meshObject.Id, out PickingEntry? entry))
            return;

        _colliderIds.Remove(entry.Body.GetInstanceId());
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
            && _entries.TryGetValue(id, out PickingEntry? entry)
        )
        {
            entry.Body.Transform = EffectiveTransform(meshObject);
        }
    }

    // Colliders follow previews so a raycast hits an object where it is drawn, not where it was
    // last committed.
    private Transform3D EffectiveTransform(Scene.MeshObject meshObject) =>
        _previews.TryGetTransform(meshObject.Id, out Transform3D preview)
            ? preview
            : meshObject.Transform;

    public void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        foreach (MeshObjectId id in _dirtyMeshes)
        {
            if (
                _scene.Objects.TryGetValue(id, out Scene.MeshObject? meshObject)
                && _entries.TryGetValue(id, out PickingEntry? entry)
            )
            {
                meshObject.Mesh.RebuildSurfaceIfDirty();
                entry.Shape.Data = CreateCollisionFaces(meshObject.Mesh.Surface);
            }
        }

        _dirtyMeshes.Clear();
    }

    public MeshObjectId? Raycast(Camera3D camera, Vector2 screenPosition)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(camera);

        Vector3 origin = camera.ProjectRayOrigin(screenPosition);
        Vector3 destination = origin + camera.ProjectRayNormal(screenPosition) * RayLength;
        using PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(
            origin,
            destination
        );
        return Raycast(query);
    }

    public MeshObjectId? Raycast(PhysicsRayQueryParameters3D query)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(query);

        query.CollisionMask = PickingCollisionLayer;
        query.CollideWithBodies = true;
        query.CollideWithAreas = false;

        PhysicsDirectSpaceState3D spaceState = _mapRoot.GetWorld3D().DirectSpaceState;
        Godot.Collections.Dictionary result = spaceState.IntersectRay(query);
        if (result.Count == 0)
            return null;

        ulong colliderId = result["collider_id"].AsUInt64();
        return _colliderIds.TryGetValue(colliderId, out MeshObjectId id) ? id : null;
    }

    private void ClearSceneState()
    {
        foreach (PickingEntry entry in _entries.Values)
            entry.Dispose();

        _entries.Clear();
        _colliderIds.Clear();
        _dirtyMeshes.Clear();
    }

    private static Vector3[] CreateCollisionFaces(MeshSurface surface)
    {
        var faces = new Vector3[surface.ElementIndices.Count];
        for (int index = 0; index < surface.ElementIndices.Count; index++)
        {
            System.Numerics.Vector3 vertex = surface.Vertices[
                surface.ElementIndices[index]
            ];
            faces[index] = new Vector3(vertex.X, vertex.Y, vertex.Z);
        }

        return faces;
    }

    private sealed class PickingEntry : IDisposable
    {
        private bool _disposed;

        public PickingEntry(
            Scene.MeshObject meshObject,
            Node3D pickingRoot,
            Transform3D transform
        )
        {
            Shape = new ConcavePolygonShape3D();
            CollisionShape = new CollisionShape3D { Shape = Shape };
            Body = new StaticBody3D
            {
                Name = $"MeshObject_{meshObject.Id.Value}",
                CollisionLayer = PickingCollisionLayer,
                CollisionMask = 0,
                Transform = transform,
            };

            try
            {
                Body.AddChild(CollisionShape);
                pickingRoot.AddChild(Body);
            }
            catch
            {
                CollisionShape.Shape = null;
                if (CollisionShape.GetParent() is null)
                    CollisionShape.Free();

                Body.Free();
                Shape.Dispose();
                throw;
            }
        }

        public StaticBody3D Body { get; }

        public CollisionShape3D CollisionShape { get; }

        public ConcavePolygonShape3D Shape { get; }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            CollisionShape.Shape = null;
            Body.Free();
            Shape.Dispose();
        }
    }
}
