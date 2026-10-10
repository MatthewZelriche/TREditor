using System;
using System.Collections.Generic;
using TRMesh.Mesh;

// A quad edge loop continues straight through each four-valence vertex. Singular vertices and
// boundaries terminate the walk; encountering an edge already collected closes the loop.
internal static class EdgeLoopSelection
{
    public static IReadOnlyList<int> Collect(SpatialMesh mesh, int seed)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        if (!mesh.IsHalfEdgeAlive(seed))
            return [];

        int twin = mesh.GetHalfEdgeData(seed).topology.TwinHalfEdge;
        if (!mesh.IsHalfEdgeAlive(twin))
            return [];

        var slots = new List<int> { Math.Min(seed, twin) };
        var visited = new HashSet<int>(slots);
        Walk(mesh, seed, slots, visited);
        Walk(mesh, twin, slots, visited);
        return slots;
    }

    private static void Walk(
        SpatialMesh mesh,
        int outgoing,
        List<int> slots,
        HashSet<int> visited
    )
    {
        while (TryGetOppositeAtVertex(mesh, outgoing, out int opposite))
        {
            HalfEdge edge = mesh.GetHalfEdgeData(opposite).topology;
            int slot = Math.Min(opposite, edge.TwinHalfEdge);
            if (!visited.Add(slot))
                return;

            slots.Add(slot);
            outgoing = edge.TwinHalfEdge;
        }
    }

    private static bool TryGetOppositeAtVertex(
        SpatialMesh mesh,
        int outgoing,
        out int opposite
    )
    {
        opposite = SpatialMesh.INVALID_HANDLE;
        if (!TryRotateAroundVertex(mesh, outgoing, out int first) || first == outgoing)
            return false;
        if (!TryRotateAroundVertex(mesh, first, out int second) || second == outgoing)
            return false;
        if (!TryRotateAroundVertex(mesh, second, out int third) || third == outgoing)
            return false;
        if (!TryRotateAroundVertex(mesh, third, out int fourth) || fourth != outgoing)
            return false;

        opposite = second;
        return true;
    }

    private static bool TryRotateAroundVertex(SpatialMesh mesh, int outgoing, out int next)
    {
        next = SpatialMesh.INVALID_HANDLE;
        if (!mesh.IsHalfEdgeAlive(outgoing))
            return false;

        int twin = mesh.GetHalfEdgeData(outgoing).topology.TwinHalfEdge;
        if (!mesh.IsHalfEdgeAlive(twin))
            return false;

        next = mesh.GetHalfEdgeData(twin).topology.NextHalfEdge;
        return mesh.IsHalfEdgeAlive(next)
            && mesh.GetHalfEdgeData(next).topology.SourceVertex
                == mesh.GetHalfEdgeData(outgoing).topology.SourceVertex;
    }
}
