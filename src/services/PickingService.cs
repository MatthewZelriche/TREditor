using System;
using System.Collections.Generic;
using Godot;

public sealed partial class PickingService : ISceneService
{
    private const uint PickingCollisionLayer = 1u << 31;
    private const float RayLength = 100_000.0f;

    private readonly Node3D _mapRoot;
    private readonly Node3D _pickingRoot;
    private readonly Dictionary<MeshObjectId, PickingEntry> _entries = [];
    private readonly Dictionary<ulong, MeshObjectId> _colliderIds = [];
    private readonly HashSet<MeshObjectId> _dirtyMeshes = [];
    private readonly PreviewState _previews;
    private Document _document;
    private bool _disposed;

    public PickingService(Document document, Node3D mapRoot, PreviewState previews)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(mapRoot);
        ArgumentNullException.ThrowIfNull(previews);

        _document = document;
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

    public void SetDocument(Document document)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(document);
        if (ReferenceEquals(_document, document))
            return;

        ClearDocumentState();
        _document = document;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        ClearDocumentState();
        _pickingRoot.Free();
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

    public void OnPreviewGeometryChanged(MeshObjectId? objectId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (objectId is MeshObjectId id && _document.TryGetObject(id, out _))
            _dirtyMeshes.Add(id);
    }

    private void AddObject(MeshObjectId id)
    {
        if (!_document.TryGetObject(id, out MeshObject meshObject))
            throw new InvalidOperationException($"Document object {id.Value} was added but is missing.");

        ArgumentNullException.ThrowIfNull(meshObject.Mesh);

        var entry = new PickingEntry(meshObject, _pickingRoot, _previews.TransformFor(meshObject));
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

    private void RemoveObject(MeshObjectId id)
    {
        _dirtyMeshes.Remove(id);
        if (!_entries.Remove(id, out PickingEntry? entry))
            return;

        _colliderIds.Remove(entry.Body.GetInstanceId());
        entry.Dispose();
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
            && _entries.TryGetValue(id, out PickingEntry? entry)
        )
        {
            entry.Body.Transform = _previews.TransformFor(meshObject);
        }
    }

    public void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        foreach (MeshObjectId id in _dirtyMeshes)
        {
            if (
                _document.TryGetObject(id, out MeshObject meshObject)
                && _entries.TryGetValue(id, out PickingEntry? entry)
            )
            {
                GDMesh mesh = _previews.MeshFor(meshObject);
                mesh.RebuildSurfaceIfDirty();
                entry.UpdateComponentGeometry(mesh);
                entry.Shape.Data = entry.ComponentGeometry.ToCollisionFaces();
            }
        }

        _dirtyMeshes.Clear();
    }

    public MeshObjectId? Raycast(Camera3D camera, Vector2 screenPosition)
    {
        return TryRaycast(camera, screenPosition, out PickingHit hit) ? hit.ObjectId : null;
    }

    public MeshObjectId? Raycast(PhysicsRayQueryParameters3D query)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(query);
        return TryIntersectRay(query, out PickingHit hit) ? hit.ObjectId : null;
    }

    public bool TryRaycast(Camera3D camera, Vector2 screenPosition, out PickingHit hit)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(camera);

        Vector3 origin = camera.ProjectRayOrigin(screenPosition);
        Vector3 destination = origin + camera.ProjectRayNormal(screenPosition) * RayLength;
        using PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(
            origin,
            destination
        );
        return TryIntersectRay(query, out hit);
    }

    private bool TryIntersectRay(PhysicsRayQueryParameters3D query, out PickingHit hit)
    {
        query.CollisionMask = PickingCollisionLayer;
        query.CollideWithBodies = true;
        query.CollideWithAreas = false;

        PhysicsDirectSpaceState3D spaceState = _mapRoot.GetWorld3D().DirectSpaceState;
        Godot.Collections.Dictionary result = spaceState.IntersectRay(query);
        if (result.Count != 0)
        {
            ulong colliderId = result["collider_id"].AsUInt64();
            if (_colliderIds.TryGetValue(colliderId, out MeshObjectId id))
            {
                hit = new PickingHit(
                    id,
                    result["position"].AsVector3(),
                    result["normal"].AsVector3()
                );
                return true;
            }
        }

        hit = default;
        return false;
    }

    private void ClearDocumentState()
    {
        foreach (PickingEntry entry in _entries.Values)
            entry.Dispose();

        _entries.Clear();
        _colliderIds.Clear();
        _dirtyMeshes.Clear();
    }

    private sealed partial class PickingEntry : IDisposable
    {
        private bool _disposed;

        public PickingEntry(
            MeshObject meshObject,
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
