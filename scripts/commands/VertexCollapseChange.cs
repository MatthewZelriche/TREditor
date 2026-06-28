#nullable enable

using System;
using System.Collections.Generic;
using System.Numerics;
using TREditorSharp;

public sealed class VertexCollapseChange : IDisposable
{
    private readonly TopologyPatch _patch;
    private bool _disposed;

    public EditorObjectId ObjectId { get; }
    public VertexHandle Survivor { get; }

    private VertexCollapseChange(
        EditorObjectId objectId,
        VertexHandle survivor,
        TopologyPatch patch
    )
    {
        ObjectId = objectId;
        Survivor = survivor;
        _patch = patch;
    }

    public static bool CanCollapse(
        SpatialMesh mesh,
        IReadOnlyList<VertexHandle> vertices,
        CollapseVerticesTarget twoVertexTarget
    )
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(vertices);
        using TopologyEditScope? edit = TryCollapse(mesh, vertices, twoVertexTarget, out _);
        return edit != null;
    }

    public static VertexCollapseChange? Collapse(
        EditorObjectId objectId,
        SpatialMesh mesh,
        IReadOnlyList<VertexHandle> vertices,
        CollapseVerticesTarget twoVertexTarget
    )
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(vertices);
        using TopologyEditScope? edit = TryCollapse(
            mesh,
            vertices,
            twoVertexTarget,
            out VertexHandle survivor
        );
        if (edit == null)
            return null;

        TopologyPatch patch = edit.Commit();
        return new VertexCollapseChange(objectId, survivor, patch);
    }

    public void ApplyBefore() => _patch.ApplyBefore();

    public void ApplyAfter() => _patch.ApplyAfter();

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _patch.Dispose();
    }

    private static TopologyEditScope? TryCollapse(
        SpatialMesh mesh,
        IReadOnlyList<VertexHandle> vertices,
        CollapseVerticesTarget twoVertexTarget,
        out VertexHandle survivor
    )
    {
        survivor = VertexHandle.Null;
        if (vertices.Count < 2)
            return null;

        HashSet<VertexHandle> uniqueVertices = [];
        Vector3 centroid = Vector3.Zero;
        foreach (VertexHandle vertex in vertices)
        {
            if (!mesh.IsVertexAlive(vertex) || !uniqueVertices.Add(vertex))
                return null;
            centroid += mesh.GetVertexPosition(vertex);
        }
        centroid /= vertices.Count;

        int survivorIndex =
            vertices.Count == 2 && twoVertexTarget == CollapseVerticesTarget.Second ? 1 : 0;
        survivor = vertices[survivorIndex];
        Vector3 destination = vertices.Count == 2 ? mesh.GetVertexPosition(survivor) : centroid;

        List<VertexHandle> remaining = [];
        foreach (VertexHandle vertex in vertices)
        {
            if (vertex != survivor)
                remaining.Add(vertex);
        }

        TopologyEditScope edit = mesh.BeginTopologyEdit(vertices);
        while (remaining.Count > 0)
        {
            bool collapsed = false;
            for (int index = 0; index < remaining.Count; index++)
            {
                if (!mesh.TryMergeVertices(remaining[index], survivor))
                    continue;

                remaining.RemoveAt(index);
                collapsed = true;
                break;
            }

            if (!collapsed)
            {
                edit.Dispose();
                survivor = VertexHandle.Null;
                return null;
            }
        }

        mesh.SetVertexPosition(survivor, destination);
        FaceUvProjector.ReprojectInitializedFacesAroundVertices(mesh, [survivor]);
        return edit;
    }
}
