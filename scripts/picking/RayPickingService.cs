using System;
using Godot;

public readonly record struct RayPickHit(
    Vector3 Position,
    Vector3 Normal,
    GodotObject Collider,
    Rid Rid,
    long ColliderId,
    int Shape
);

public sealed class RayPickingService
{
    private const float DefaultMaxDistance = 10000.0f;
    private const float GridPlaneY = 0.0f;
    private const float GridRayEpsilon = 0.000001f;

    private readonly World3D _world;

    public RayPickingService(World3D world)
    {
        ArgumentNullException.ThrowIfNull(world);

        _world = world;
    }

    public bool TryPick(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        out RayPickHit hit,
        float maxDistance = DefaultMaxDistance,
        uint collisionMask = uint.MaxValue
    )
    {
        hit = default;

        if (rayDirection.IsZeroApprox() || maxDistance <= 0.0f)
        {
            return false;
        }

        PhysicsDirectSpaceState3D spaceState = _world.DirectSpaceState;
        if (spaceState == null)
        {
            return false;
        }

        Vector3 rayEnd = rayOrigin + rayDirection.Normalized() * maxDistance;
        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(
            rayOrigin,
            rayEnd,
            collisionMask
        );
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;

        Godot.Collections.Dictionary result = spaceState.IntersectRay(query);
        if (result.Count == 0)
        {
            return false;
        }

        hit = new RayPickHit(
            result["position"].AsVector3(),
            result["normal"].AsVector3(),
            result["collider"].AsGodotObject(),
            result["rid"].AsRid(),
            result["collider_id"].AsInt64(),
            result["shape"].AsInt32()
        );
        return true;
    }

    /// <summary>
    /// Finds the world-space intersection between <paramref name="rayOrigin"/> /
    /// <paramref name="rayDirection"/> and the editor grid plane at Y=0.
    /// This ignores physics objects entirely; callers typically use it as a fallback after
    /// <see cref="TryPick"/> does not find a mesh collider hit.
    /// </summary>
    public bool TryPickGrid(Vector3 rayOrigin, Vector3 rayDirection, out Vector3 position)
    {
        position = default;

        if (rayDirection.IsZeroApprox())
        {
            return false;
        }

        if (Mathf.Abs(rayDirection.Y) < GridRayEpsilon)
        {
            return false;
        }

        float t = (GridPlaneY - rayOrigin.Y) / rayDirection.Y;
        if (t < 0.0f)
        {
            return false;
        }

        position = rayOrigin + rayDirection * t;
        return true;
    }
}
