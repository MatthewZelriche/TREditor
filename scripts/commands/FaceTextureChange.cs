#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TREditorSharp;

public readonly record struct FaceCornerUvState(FaceCornerHandle Corner, Vector2 Uv);

public sealed class FaceTextureState
{
    public int MaterialSlot { get; }
    public bool UvsInitialized { get; }
    public IReadOnlyList<FaceCornerUvState> CornerUvs { get; }

    public FaceTextureState(
        int materialSlot,
        bool uvsInitialized,
        IReadOnlyList<FaceCornerUvState> cornerUvs
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegative(materialSlot);
        ArgumentNullException.ThrowIfNull(cornerUvs);

        MaterialSlot = materialSlot;
        UvsInitialized = uvsInitialized;
        CornerUvs = Array.AsReadOnly(cornerUvs.ToArray());
    }

    public static FaceTextureState Capture(SpatialMesh mesh, FaceHandle face)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        List<FaceCornerUvState> corners = [];
        foreach (FaceCornerHandle corner in mesh.HalfEdgesAroundFace(face))
            corners.Add(new FaceCornerUvState(corner, mesh.GetFaceCornerUv(corner)));

        return new FaceTextureState(
            mesh.GetFaceMaterialSlot(face),
            mesh.AreFaceUvsInitialized(face),
            corners
        );
    }

    public void Apply(SpatialMesh mesh, FaceHandle face)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ValidateCurrentFaceLoop(mesh, face);

        foreach (FaceCornerUvState corner in CornerUvs)
            mesh.SetFaceCornerUv(corner.Corner, corner.Uv);
        mesh.SetFaceMaterialSlot(face, MaterialSlot);
        mesh.SetFaceUvsInitialized(face, UvsInitialized);
    }

    private void ValidateCurrentFaceLoop(SpatialMesh mesh, FaceHandle face)
    {
        int index = 0;
        foreach (FaceCornerHandle corner in mesh.HalfEdgesAroundFace(face))
        {
            if (index >= CornerUvs.Count || CornerUvs[index].Corner != corner)
            {
                throw new InvalidOperationException(
                    "Cannot apply face texture state after the polygon face loop has changed."
                );
            }
            index++;
        }

        if (index != CornerUvs.Count)
        {
            throw new InvalidOperationException(
                "Cannot apply face texture state after the polygon face loop has changed."
            );
        }
    }
}

/// <summary>
/// Complete before/after state for one polygon-face texture application.
/// </summary>
public sealed class FaceTextureChange
{
    public FaceHandle Face { get; }
    public FaceTextureState Before { get; }
    public FaceTextureState After { get; }

    private FaceTextureChange(FaceHandle face, FaceTextureState before, FaceTextureState after)
    {
        Face = face;
        Before = before;
        After = after;
    }

    public static FaceTextureChange? Create(SpatialMesh mesh, FaceHandle face, int materialSlot)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (materialSlot <= SpatialMesh.UntexturedMaterialSlot || !IsLiveFace(mesh, face))
            return null;

        FaceTextureState before = FaceTextureState.Capture(mesh, face);
        if (before.UvsInitialized && before.MaterialSlot == materialSlot)
            return null;

        if (before.UvsInitialized)
        {
            return new FaceTextureChange(
                face,
                before,
                new FaceTextureState(materialSlot, true, before.CornerUvs)
            );
        }

        List<ProjectedFaceCornerUv> projected = [];
        if (!FaceUvProjector.TryProject(mesh, face, projected))
            return null;

        FaceTextureState after = new(
            materialSlot,
            true,
            projected.Select(corner => new FaceCornerUvState(corner.Corner, corner.Uv)).ToArray()
        );
        return new FaceTextureChange(face, before, after);
    }

    public void Apply(SpatialMesh mesh) => After.Apply(mesh, Face);

    public void Revert(SpatialMesh mesh) => Before.Apply(mesh, Face);

    private static bool IsLiveFace(SpatialMesh mesh, FaceHandle face)
    {
        foreach (FaceHandle liveFace in mesh.EnumerateLiveFaces())
        {
            if (liveFace == face)
                return true;
        }

        return false;
    }
}
