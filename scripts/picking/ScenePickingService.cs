using System;
using System.Collections.Generic;
using Godot;

public readonly record struct RayPickHit(
    Vector3 Position,
    Vector3 Normal,
    GodotObject Collider,
    Rid Rid,
    long ColliderId,
    int Shape
);

public sealed class ScenePickingService
{
    private const float DefaultMaxDistance = 10000.0f;
    private const float GridPlaneY = 0.0f;
    private const float GridRayEpsilon = 0.000001f;
    private const float DepthEpsilon = 0.000001f;

    private readonly World3D _world;
    private readonly CapsuleShape3D _broadphaseCapsule = new();
    private readonly PhysicsShapeQueryParameters3D _broadphaseQuery = new()
    {
        CollideWithAreas = false,
        CollideWithBodies = true,
    };
    private readonly HashSet<TRMeshGD> _candidateScratch = [];

    public ScenePickingService(World3D world)
    {
        ArgumentNullException.ThrowIfNull(world);

        _world = world;
    }

    /// <summary>Radius used for fuzzy object broad-phase capsule traces.</summary>
    public float BroadphaseCapsuleRadius { get; set; } = 0.1f;

    /// <summary>Local-space sphere radius used when picking mesh vertices.</summary>
    public float VertexPickRadius { get; set; } = 0.11f;

    /// <summary>Local-space capsule radius used when picking mesh edges.</summary>
    public float EdgePickRadius { get; set; } = 0.09f;

    /// <summary>Performs an exact physics ray pick and returns raw Godot hit data.</summary>
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
    /// Fuzzy-picks the nearest <see cref="TRMeshGD"/> whose collider overlaps the pick capsule.
    /// </summary>
    public bool TryPickObject(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        out ScenePickHit hit,
        float maxDistance = DefaultMaxDistance,
        uint collisionMask = uint.MaxValue
    )
    {
        return TryPickScene(
            rayOrigin,
            rayDirection,
            out hit,
            ScenePickElementFilter.Object,
            false,
            maxDistance,
            collisionMask
        );
    }

    /// <summary>
    /// Fuzzy-picks candidate meshes, then resolves the requested mesh object or component.
    /// </summary>
    public bool TryPickScene(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        out ScenePickHit hit,
        ScenePickElementFilter filter = ScenePickElementFilter.AnyComponent,
        bool xRayMode = false,
        float maxDistance = DefaultMaxDistance,
        uint collisionMask = uint.MaxValue
    )
    {
        hit = ScenePickHit.None;

        if (rayDirection.IsZeroApprox() || maxDistance <= 0.0f)
        {
            return false;
        }

        PhysicsDirectSpaceState3D spaceState = _world.DirectSpaceState;
        if (spaceState == null)
        {
            return false;
        }

        Vector3 rayDirectionUnit = rayDirection.Normalized();
        GatherBroadphaseCandidates(
            spaceState,
            rayOrigin,
            rayDirectionUnit,
            maxDistance,
            collisionMask
        );

        if (_candidateScratch.Count == 0)
        {
            return false;
        }

        ScenePickHit objectHit = ScenePickHit.None;
        ScenePickHit vertexHit = ScenePickHit.None;
        ScenePickHit edgeHit = ScenePickHit.None;
        ScenePickHit faceHit = ScenePickHit.None;

        foreach (TRMeshGD candidate in _candidateScratch)
        {
            TryPickCandidateComponents(
                candidate,
                rayOrigin,
                rayDirectionUnit,
                maxDistance,
                out ScenePickHit vertex,
                out ScenePickHit edge,
                out ScenePickHit face
            );

            if (filter == ScenePickElementFilter.Object)
            {
                UpdateBestHit(
                    ObjectPickResolver.ResolveCandidate(candidate, vertex, edge, face),
                    ref objectHit
                );
                continue;
            }

            UpdateBestHit(vertex, ref vertexHit);
            UpdateBestHit(edge, ref edgeHit);
            UpdateBestHit(face, ref faceHit);
        }

        hit = ResolvePick(filter, xRayMode, objectHit, vertexHit, edgeHit, faceHit);
        return hit.HasHit;
    }

    /// <summary>Finds the world-space intersection between a ray and the Y=0 editor grid.</summary>
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

    private void GatherBroadphaseCandidates(
        PhysicsDirectSpaceState3D spaceState,
        Vector3 rayOrigin,
        Vector3 rayDirectionUnit,
        float maxDistance,
        uint collisionMask
    )
    {
        _candidateScratch.Clear();

        float radius = Mathf.Max(
            BroadphaseCapsuleRadius,
            Mathf.Max(VertexPickRadius, EdgePickRadius)
        );
        if (radius <= 0.0f)
        {
            return;
        }

        _broadphaseCapsule.Radius = radius;
        _broadphaseCapsule.Height = Mathf.Max(maxDistance + 2.0f * radius, 2.0f * radius);
        _broadphaseQuery.Shape = _broadphaseCapsule;
        _broadphaseQuery.CollisionMask = collisionMask;
        // Transform capsule along ray direction so it extends in the direction of the pick ray.
        _broadphaseQuery.Transform = new Transform3D(
            BasisFromYAxis(rayDirectionUnit),
            rayOrigin + rayDirectionUnit * (maxDistance * 0.5f)
        );

        Godot.Collections.Array<Godot.Collections.Dictionary> hits = spaceState.IntersectShape(
            _broadphaseQuery,
            128
        );
        foreach (Godot.Collections.Dictionary broadphaseHit in hits)
        {
            GodotObject collider = broadphaseHit["collider"].AsGodotObject();
            if (TryGetMeshNode(collider, out TRMeshGD meshNode))
            {
                _candidateScratch.Add(meshNode);
            }
        }
    }

    private bool TryPickCandidateComponents(
        TRMeshGD candidate,
        Vector3 rayOrigin,
        Vector3 rayDirectionUnit,
        float maxDistance,
        out ScenePickHit vertex,
        out ScenePickHit edge,
        out ScenePickHit face
    )
    {
        Transform3D inverseTransform = candidate.GlobalTransform.AffineInverse();
        Vector3 localOrigin = inverseTransform * rayOrigin;
        Vector3 localEnd = inverseTransform * (rayOrigin + rayDirectionUnit * maxDistance);
        Vector3 localSegment = localEnd - localOrigin;
        float localMaxDistance = localSegment.Length();

        if (localMaxDistance <= DepthEpsilon)
        {
            vertex = ScenePickHit.None;
            edge = ScenePickHit.None;
            face = ScenePickHit.None;
            return false;
        }

        Vector3 localDirection = localSegment / localMaxDistance;
        bool hasHit = MeshComponentPicker.TryPickComponents(
            candidate,
            localOrigin,
            localDirection,
            localMaxDistance,
            VertexPickRadius,
            EdgePickRadius,
            out vertex,
            out edge,
            out face
        );

        vertex = ToWorldHit(
            vertex,
            candidate.GlobalTransform,
            rayOrigin,
            rayDirectionUnit,
            maxDistance
        );
        edge = ToWorldHit(
            edge,
            candidate.GlobalTransform,
            rayOrigin,
            rayDirectionUnit,
            maxDistance
        );
        face = ToWorldHit(
            face,
            candidate.GlobalTransform,
            rayOrigin,
            rayDirectionUnit,
            maxDistance
        );
        return hasHit;
    }

    private static ScenePickHit ToWorldHit(
        ScenePickHit localHit,
        Transform3D transform,
        Vector3 rayOrigin,
        Vector3 rayDirectionUnit,
        float maxDistance
    )
    {
        if (!localHit.HasHit)
        {
            return ScenePickHit.None;
        }

        Vector3 worldPosition = transform * localHit.Position;
        float worldDistance = (worldPosition - rayOrigin).Dot(rayDirectionUnit);
        if (worldDistance < 0.0f || worldDistance > maxDistance)
        {
            return ScenePickHit.None;
        }

        return localHit.Kind switch
        {
            ScenePickElementKind.Vertex => ScenePickHit.VertexHit(
                localHit.Mesh,
                localHit.Vertex,
                worldPosition,
                worldDistance
            ),
            ScenePickElementKind.Edge => ScenePickHit.EdgeHit(
                localHit.Mesh,
                localHit.Edge,
                worldPosition,
                worldDistance
            ),
            ScenePickElementKind.Face => ScenePickHit.FaceHit(
                localHit.Mesh,
                localHit.Face,
                worldPosition,
                worldDistance
            ),
            _ => ScenePickHit.None,
        };
    }

    private static ScenePickHit ResolvePick(
        ScenePickElementFilter filter,
        bool xRayMode,
        ScenePickHit objectHit,
        ScenePickHit vertexHit,
        ScenePickHit edgeHit,
        ScenePickHit faceHit
    )
    {
        return filter switch
        {
            ScenePickElementFilter.Object => objectHit,
            ScenePickElementFilter.Vertex => vertexHit,
            ScenePickElementFilter.Edge => edgeHit,
            ScenePickElementFilter.Face => faceHit,
            _ => ResolveAnyComponent(xRayMode, vertexHit, edgeHit, faceHit),
        };
    }

    private static ScenePickHit ResolveAnyComponent(
        bool xRayMode,
        ScenePickHit vertexHit,
        ScenePickHit edgeHit,
        ScenePickHit faceHit
    )
    {
        if (xRayMode)
        {
            return ResolveAnyComponentXRay(vertexHit, edgeHit, faceHit);
        }

        ScenePickHit best = ScenePickHit.None;
        UpdateBestHit(vertexHit, ref best);
        UpdateBestHit(edgeHit, ref best);
        UpdateBestHit(faceHit, ref best);
        return best;
    }

    private static ScenePickHit ResolveAnyComponentXRay(
        ScenePickHit vertexHit,
        ScenePickHit edgeHit,
        ScenePickHit faceHit
    )
    {
        // XRay mode ignores cross-component depth so hidden vertices/edges can win over faces.
        if (vertexHit.HasHit)
        {
            return vertexHit;
        }

        if (edgeHit.HasHit)
        {
            return edgeHit;
        }

        return faceHit;
    }

    // Determines whether candidate is a "better" hit than current best. "Better" is defined by
    // both distance and component type
    private static void UpdateBestHit(ScenePickHit candidate, ref ScenePickHit best)
    {
        if (!candidate.HasHit)
        {
            return;
        }

        if (!best.HasHit || candidate.Distance < best.Distance - DepthEpsilon)
        {
            best = candidate;
            return;
        }

        if (
            Mathf.Abs(candidate.Distance - best.Distance) <= DepthEpsilon
            && PickPriority(candidate.Kind) > PickPriority(best.Kind)
        )
        {
            best = candidate;
        }
    }

    private static int PickPriority(ScenePickElementKind kind) =>
        kind switch
        {
            ScenePickElementKind.Vertex => 3,
            ScenePickElementKind.Edge => 2,
            ScenePickElementKind.Face => 1,
            _ => 0,
        };

    private static bool TryGetMeshNode(GodotObject collider, out TRMeshGD meshNode)
    {
        meshNode = null;

        if (collider is MeshCollider meshCollider && meshCollider.GetParent() is TRMeshGD parent)
        {
            meshNode = parent;
            return true;
        }

        return false;
    }

    private static Basis BasisFromYAxis(Vector3 yAxis)
    {
        yAxis = yAxis.Normalized();
        Vector3 hint = Mathf.Abs(yAxis.Dot(Vector3.Up)) > 0.99f ? Vector3.Right : Vector3.Up;
        Vector3 xAxis = hint.Cross(yAxis);
        if (xAxis.LengthSquared() < 0.00000001f)
        {
            xAxis = Vector3.Forward.Cross(yAxis);
        }

        xAxis = xAxis.Normalized();
        Vector3 zAxis = xAxis.Cross(yAxis).Normalized();
        return new Basis(xAxis, yAxis, zAxis);
    }
}
