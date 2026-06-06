using System;
using System.Collections.Generic;
using Godot;
using TREditorSharp;
using NumericVector3 = System.Numerics.Vector3;

// Centralizes committed scene-node mutation for now. This is intentionally a step toward
// a proper editor document/model layer, where commands would mutate model state and the
// Godot scene would become a synchronized view of that model.
public sealed class EditorSceneService : IDisposable
{
    private readonly Node3D _worldRoot;
    private readonly Dictionary<EditorObjectId, TRMeshGD> _meshNodes = [];

    private bool _disposed;

    public EditorSceneService(Node3D worldRoot)
    {
        ArgumentNullException.ThrowIfNull(worldRoot);

        _worldRoot = worldRoot;
    }

    public void CreateMeshObject(EditorObjectId objectId, SpatialMesh mesh, string displayName)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        if (_meshNodes.TryGetValue(objectId, out TRMeshGD existingNode))
        {
            existingNode.ObjectId = objectId;
            if (existingNode.GetParent() == null)
            {
                _worldRoot.AddChild(existingNode);
            }

            return;
        }

        TRMeshGD meshNode = new() { Name = displayName, ObjectId = objectId };
        meshNode.TakeMesh(mesh);
        _meshNodes.Add(objectId, meshNode);
        _worldRoot.AddChild(meshNode);
    }

    public void RemoveMeshObject(EditorObjectId objectId)
    {
        if (!_meshNodes.TryGetValue(objectId, out TRMeshGD meshNode))
        {
            return;
        }

        Node parent = meshNode.GetParent();
        parent?.RemoveChild(meshNode);
    }

    public IEnumerable<KeyValuePair<EditorObjectId, TRMeshGD>> EnumerateMeshObjects() => _meshNodes;

    public bool TryGetSelectionCenter(SelectionSnapshot selection, out Vector3 center)
    {
        center = Vector3.Zero;
        if (selection.IsEmpty)
        {
            return false;
        }

        int count = 0;
        foreach (SelectionTarget target in selection.Targets)
        {
            if (!TryGetSelectionTargetCenter(target, out Vector3 targetCenter))
            {
                continue;
            }

            center += targetCenter;
            count++;
        }

        if (count == 0)
        {
            center = Vector3.Zero;
            return false;
        }

        center /= count;
        return true;
    }

    public void TranslateSelection(SelectionSnapshot selection, Vector3 worldDelta)
    {
        if (selection.IsEmpty || worldDelta.IsZeroApprox())
        {
            return;
        }

        HashSet<EditorObjectId> translatedObjects = [];
        Dictionary<EditorObjectId, HashSet<VertexHandle>> componentVertices = [];

        foreach (SelectionTarget target in selection.Targets)
        {
            if (target.Kind == ScenePickElementKind.Object)
            {
                if (_meshNodes.TryGetValue(target.ObjectId, out TRMeshGD meshNode))
                {
                    meshNode.GlobalPosition += worldDelta;
                    translatedObjects.Add(target.ObjectId);
                }

                continue;
            }

            if (translatedObjects.Contains(target.ObjectId))
            {
                continue;
            }

            if (!_meshNodes.TryGetValue(target.ObjectId, out TRMeshGD componentMeshNode))
            {
                continue;
            }

            if (!componentVertices.TryGetValue(target.ObjectId, out HashSet<VertexHandle> vertices))
            {
                vertices = [];
                componentVertices[target.ObjectId] = vertices;
            }

            AddTargetVertices(componentMeshNode.SourceMesh, target, vertices);
        }

        foreach ((EditorObjectId objectId, HashSet<VertexHandle> vertices) in componentVertices)
        {
            if (translatedObjects.Contains(objectId) || vertices.Count == 0)
            {
                continue;
            }

            if (!_meshNodes.TryGetValue(objectId, out TRMeshGD meshNode))
            {
                continue;
            }

            Vector3 localDelta = meshNode.GlobalTransform.Basis.Inverse() * worldDelta;
            NumericVector3 numericDelta = ToNumericVector3(localDelta);
            SpatialMesh mesh = meshNode.SourceMesh;

            foreach (VertexHandle vertex in vertices)
            {
                mesh.SetVertexPosition(vertex, mesh.GetVertexPosition(vertex) + numericDelta);
            }

            meshNode.Rebuild();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (TRMeshGD meshNode in _meshNodes.Values)
        {
            Node parent = meshNode.GetParent();
            parent?.RemoveChild(meshNode);
            meshNode.QueueFree();
        }

        _meshNodes.Clear();
    }

    private bool TryGetSelectionTargetCenter(SelectionTarget target, out Vector3 center)
    {
        center = Vector3.Zero;
        if (!_meshNodes.TryGetValue(target.ObjectId, out TRMeshGD meshNode))
        {
            return false;
        }

        SpatialMesh mesh = meshNode.SourceMesh;

        switch (target.Kind)
        {
            case ScenePickElementKind.Object:
                return TryGetMeshBoundsCenter(meshNode, out center);
            case ScenePickElementKind.Vertex:
                center =
                    meshNode.GlobalTransform
                    * ToGodotVector3(mesh.GetVertexPosition(target.Vertex));
                return true;
            case ScenePickElementKind.Edge:
                if (!TryGetEdgeCenter(mesh, target.Edge, out Vector3 edgeCenter))
                {
                    return false;
                }

                center = meshNode.GlobalTransform * edgeCenter;
                return true;
            case ScenePickElementKind.Face:
                if (!TryGetFaceCenter(mesh, target.Face, out Vector3 faceCenter))
                {
                    return false;
                }

                center = meshNode.GlobalTransform * faceCenter;
                return true;
            default:
                return false;
        }
    }

    private static bool TryGetMeshBoundsCenter(TRMeshGD meshNode, out Vector3 center)
    {
        center = Vector3.Zero;
        bool hasVertex = false;
        Vector3 min = Vector3.Zero;
        Vector3 max = Vector3.Zero;

        foreach (VertexHandle vertex in meshNode.SourceMesh.EnumerateLiveVertices())
        {
            Vector3 position = ToGodotVector3(meshNode.SourceMesh.GetVertexPosition(vertex));
            if (!hasVertex)
            {
                min = position;
                max = position;
                hasVertex = true;
                continue;
            }

            min = new Vector3(
                Mathf.Min(min.X, position.X),
                Mathf.Min(min.Y, position.Y),
                Mathf.Min(min.Z, position.Z)
            );
            max = new Vector3(
                Mathf.Max(max.X, position.X),
                Mathf.Max(max.Y, position.Y),
                Mathf.Max(max.Z, position.Z)
            );
        }

        if (!hasVertex)
        {
            return false;
        }

        center = meshNode.GlobalTransform * ((min + max) * 0.5f);
        return true;
    }

    private static void AddTargetVertices(
        SpatialMesh mesh,
        SelectionTarget target,
        HashSet<VertexHandle> vertices
    )
    {
        switch (target.Kind)
        {
            case ScenePickElementKind.Vertex:
                vertices.Add(target.Vertex);
                break;
            case ScenePickElementKind.Edge:
                AddEdgeVertices(mesh, target.Edge, vertices);
                break;
            case ScenePickElementKind.Face:
                foreach (HalfEdgeHandle halfEdge in mesh.HalfEdgesAroundFace(target.Face))
                {
                    vertices.Add(mesh.GetHalfEdge(halfEdge).Origin);
                }

                break;
        }
    }

    private static void AddEdgeVertices(
        SpatialMesh mesh,
        HalfEdgeHandle edge,
        HashSet<VertexHandle> vertices
    )
    {
        HalfEdge halfEdge = mesh.GetHalfEdge(edge);
        HalfEdge twin = mesh.GetHalfEdge(halfEdge.Twin);
        vertices.Add(halfEdge.Origin);
        vertices.Add(twin.Origin);
    }

    private static bool TryGetEdgeCenter(SpatialMesh mesh, HalfEdgeHandle edge, out Vector3 center)
    {
        center = Vector3.Zero;

        try
        {
            HalfEdge halfEdge = mesh.GetHalfEdge(edge);
            HalfEdge twin = mesh.GetHalfEdge(halfEdge.Twin);
            center =
                (
                    ToGodotVector3(mesh.GetVertexPosition(halfEdge.Origin))
                    + ToGodotVector3(mesh.GetVertexPosition(twin.Origin))
                ) * 0.5f;
            return true;
        }
        catch (Exception exception)
            when (exception
                    is ArgumentException
                        or InvalidOperationException
                        or KeyNotFoundException
                        or IndexOutOfRangeException
            )
        {
            return false;
        }
    }

    private static bool TryGetFaceCenter(SpatialMesh mesh, FaceHandle face, out Vector3 center)
    {
        center = Vector3.Zero;
        int count = 0;

        try
        {
            foreach (HalfEdgeHandle halfEdge in mesh.HalfEdgesAroundFace(face))
            {
                VertexHandle vertex = mesh.GetHalfEdge(halfEdge).Origin;
                center += ToGodotVector3(mesh.GetVertexPosition(vertex));
                count++;
            }
        }
        catch (Exception exception)
            when (exception
                    is ArgumentException
                        or InvalidOperationException
                        or KeyNotFoundException
                        or IndexOutOfRangeException
            )
        {
            center = Vector3.Zero;
            return false;
        }

        if (count == 0)
        {
            return false;
        }

        center /= count;
        return true;
    }

    private static Vector3 ToGodotVector3(NumericVector3 value) => new(value.X, value.Y, value.Z);

    private static NumericVector3 ToNumericVector3(Vector3 value) => new(value.X, value.Y, value.Z);
}
