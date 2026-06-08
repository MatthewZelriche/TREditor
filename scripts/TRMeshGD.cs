using System;
using System.Collections.Generic;
using Godot;
using TREditorSharp;

public partial class TRMeshGD : Node3D
{
    [Flags]
    private enum RebuildTarget
    {
        Render = 1 << 0,
        Collision = 1 << 1,
        All = Render | Collision,
    }

    public EditorObjectId ObjectId { get; internal set; }

    public SpatialMesh SourceMesh { get; private set; } = new();

    public MeshRenderable Renderable { get; private set; }

    public MeshCollider Collider { get; private set; }

    // Scratch buffers are cleared only when rebuilding their corresponding output. They are never
    // authoritative mesh data.
    private readonly MeshRenderData _rebuildScratchRenderData = new();
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

    /// <summary>
    /// Rebuilds both visible render geometry and collision geometry after a geometry or topology
    /// change.
    /// </summary>
    public void Rebuild() => Rebuild(RebuildTarget.All);

    /// <summary>
    /// Rebuilds only visible render geometry. Use this for UV, material, and other visual-only
    /// changes that cannot affect picking or collision.
    /// </summary>
    public void RebuildRender() => Rebuild(RebuildTarget.Render);

    /// <summary>
    /// Rebuilds only collision geometry.
    /// </summary>
    public void RebuildCollision() => Rebuild(RebuildTarget.Collision);

    private void Rebuild(RebuildTarget targets)
    {
        EnsureChildren();
        bool rebuildRender = (targets & RebuildTarget.Render) != 0;
        bool rebuildCollision = (targets & RebuildTarget.Collision) != 0;
        ClearRebuildScratch(rebuildRender, rebuildCollision);

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

                if (rebuildRender)
                {
                    // TRMesh stores outward faces CCW; Godot expects the opposite winding for
                    // rendering.
                    MeshRenderDataBuilder.AppendTriangle(
                        SourceMesh,
                        _rebuildScratchRenderData,
                        a,
                        c,
                        b
                    );
                }

                if (rebuildCollision)
                {
                    MeshCollider.AppendRebuildTriangle(
                        SourceMesh,
                        _rebuildScratchColliderFaces,
                        a,
                        b,
                        c
                    );
                }
            }
        }

        if (rebuildRender)
        {
            Renderable.Rebuild(_rebuildScratchRenderData);
        }

        if (rebuildCollision)
        {
            Collider.Rebuild(_rebuildScratchColliderFaces);
        }
    }

    // TODO: Investigate this a little further, there's probably a less goofy way to do this.
    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
        {
            SourceMesh.Dispose();
        }
    }

    private void ClearRebuildScratch(bool clearRender, bool clearCollision)
    {
        if (clearRender)
        {
            _rebuildScratchRenderData.Clear();
        }

        if (clearCollision)
        {
            _rebuildScratchColliderFaces.Clear();
        }

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
