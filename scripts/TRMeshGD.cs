using System;
using System.Collections.Generic;
using Godot;
using TREditorSharp;

public partial class TRMeshGD : Node3D
{
    public EditorObjectId ObjectId { get; internal set; }

    public SpatialMesh SourceMesh { get; private set; } = new();

    public MeshRenderable Renderable { get; private set; }

    public MeshCollider Collider { get; private set; }

    // Rebuild scratch only — cleared at the start of each Rebuild(); not authoritative mesh data.
    private readonly List<Vector3> _rebuildScratchRenderVertices = [];
    private readonly List<Vector3> _rebuildScratchRenderNormals = [];
    private readonly List<int> _rebuildScratchRenderIndices = [];
    private readonly List<Vector3> _rebuildScratchColliderFaces = [];
    private readonly List<FaceCornerHandle> _rebuildScratchFaceCorners = [];

    public override void _Ready()
    {
        EnsureChildren();
    }

    public void TakeMesh(SpatialMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        EnsureChildren();

        if (!ReferenceEquals(SourceMesh, mesh))
        {
            SourceMesh.Dispose();
            SourceMesh = mesh;
        }

        Rebuild();
    }

    public void SetObjectSelected(bool selected)
    {
        EnsureChildren();
        Renderable.SetSelected(selected);
    }

    // Constructs both render and collider data from half edge mesh data.
    public void Rebuild()
    {
        EnsureChildren();
        ClearRebuildScratch();

        foreach (var face in SourceMesh.EnumerateLiveFaces())
        {
            _rebuildScratchFaceCorners.Clear();

            if (!SourceMesh.TriangulateFace(face, _rebuildScratchFaceCorners))
            {
                GD.PushWarning($"TRMeshGD skipped face {face}: triangulation failed.");
                continue;
            }

            for (int i = 0; i < _rebuildScratchFaceCorners.Count; i += 3)
            {
                FaceCornerHandle a = _rebuildScratchFaceCorners[i];
                FaceCornerHandle b = _rebuildScratchFaceCorners[i + 1];
                FaceCornerHandle c = _rebuildScratchFaceCorners[i + 2];

                // TRMesh stores outward faces CCW; Godot expects the opposite winding for rendering.
                MeshRenderable.AppendRebuildTriangle(
                    SourceMesh,
                    _rebuildScratchRenderVertices,
                    _rebuildScratchRenderNormals,
                    _rebuildScratchRenderIndices,
                    a,
                    c,
                    b
                );
                MeshCollider.AppendRebuildTriangle(
                    SourceMesh,
                    _rebuildScratchColliderFaces,
                    a,
                    b,
                    c
                );
            }
        }

        Renderable.Rebuild(
            _rebuildScratchRenderVertices,
            _rebuildScratchRenderNormals,
            _rebuildScratchRenderIndices
        );
        Collider.Rebuild(_rebuildScratchColliderFaces);
    }

    // TODO: Investigate this a little further, there's probably a less goofy way to do this.
    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
        {
            SourceMesh.Dispose();
        }
    }

    private void ClearRebuildScratch()
    {
        _rebuildScratchRenderVertices.Clear();
        _rebuildScratchRenderNormals.Clear();
        _rebuildScratchRenderIndices.Clear();
        _rebuildScratchColliderFaces.Clear();
        _rebuildScratchFaceCorners.Clear();
    }

    private void EnsureChildren()
    {
        if (Renderable != null && Collider != null)
        {
            return;
        }

        Renderable = new MeshRenderable { Name = "Renderable" };
        Collider = new MeshCollider { Name = "Collider" };
        AddChild(Renderable);
        AddChild(Collider);
    }
}
