using System;
using System.Collections.Generic;
using TRMesh.Mesh;

// Boundary seeds follow their boundary loop. Interior loops cross even valence vertices and follow
// one adjacent face through odd or valence-two vertices, including onto boundary edges. Interior
// walks stop at higher even-valence boundary vertices. Repeated edges close or merge the walk.
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

        bool followBoundary = IsBoundaryEdge(mesh, seed);
        var slots = new List<int> { Math.Min(seed, twin) };
        var visited = new HashSet<int>(slots);
        Walk(mesh, seed, followBoundary, slots, visited);
        Walk(mesh, twin, followBoundary, slots, visited);
        return slots;
    }

    private static void Walk(
        SpatialMesh mesh,
        int backEdge,
        bool followBoundary,
        List<int> slots,
        HashSet<int> visited
    )
    {
        int markedFace = SpatialMesh.INVALID_HANDLE;
        while (
            TryGetNextAtVertex(mesh, backEdge, followBoundary, ref markedFace, out int next)
        )
        {
            HalfEdge edge = mesh.GetHalfEdgeData(next).topology;
            int slot = Math.Min(next, edge.TwinHalfEdge);
            if (!visited.Add(slot))
                return;

            slots.Add(slot);
            backEdge = edge.TwinHalfEdge;
        }
    }

    private static bool TryGetNextAtVertex(
        SpatialMesh mesh,
        int backEdge,
        bool followBoundary,
        ref int markedFace,
        out int next
    )
    {
        next = SpatialMesh.INVALID_HANDLE;
        if (!mesh.IsHalfEdgeAlive(backEdge))
            return false;

        HalfEdge back = mesh.GetHalfEdgeData(backEdge).topology;
        if (!mesh.IsHalfEdgeAlive(back.TwinHalfEdge))
            return false;

        HalfEdge twin = mesh.GetHalfEdgeData(back.TwinHalfEdge).topology;
        bool backIsOpen = !mesh.IsFaceAlive(back.AdjacentFace);
        bool twinIsOpen = !mesh.IsFaceAlive(twin.AdjacentFace);
        if (followBoundary)
        {
            if (!backIsOpen && !twinIsOpen)
                return false;

            markedFace = SpatialMesh.INVALID_HANDLE;
            next = backIsOpen
                ? mesh.GetHalfEdgeData(back.PrevHalfEdge).topology.TwinHalfEdge
                : twin.NextHalfEdge;
            return IsOutgoingFromSameVertex(mesh, backEdge, next);
        }

        if (!TryGetValence(mesh, backEdge, out int valence, out bool isBoundary))
            return false;

        if (valence % 2 == 0 && valence != 2)
        {
            if (isBoundary)
                return false;

            markedFace = SpatialMesh.INVALID_HANDLE;
            next = backEdge;
            for (int step = 0; step < valence / 2; step++)
            {
                if (!TryRotateAroundVertex(mesh, next, out next))
                    return false;
            }
            return next != backEdge;
        }

        if (
            !mesh.IsFaceAlive(markedFace)
            || (markedFace != back.AdjacentFace && markedFace != twin.AdjacentFace)
        )
        {
            if (isBoundary)
                return false;

            bool backFaceIsLive = mesh.IsFaceAlive(back.AdjacentFace);
            bool twinFaceIsLive = mesh.IsFaceAlive(twin.AdjacentFace);
            if (!backFaceIsLive && !twinFaceIsLive)
                return false;

            markedFace = backFaceIsLive && twinFaceIsLive
                ? Math.Min(back.AdjacentFace, twin.AdjacentFace)
                : backFaceIsLive
                    ? back.AdjacentFace
                    : twin.AdjacentFace;
        }

        next = markedFace == back.AdjacentFace
            ? mesh.GetHalfEdgeData(back.PrevHalfEdge).topology.TwinHalfEdge
            : twin.NextHalfEdge;
        return IsOutgoingFromSameVertex(mesh, backEdge, next);
    }

    private static bool TryGetValence(
        SpatialMesh mesh,
        int start,
        out int valence,
        out bool isBoundary
    )
    {
        valence = 0;
        isBoundary = false;
        int current = start;
        do
        {
            valence++;
            isBoundary |= IsBoundaryEdge(mesh, current);
            if (valence > mesh.HalfEdgeCount || !TryRotateAroundVertex(mesh, current, out current))
                return false;
        } while (current != start);

        return true;
    }

    private static bool IsBoundaryEdge(SpatialMesh mesh, int halfEdge)
    {
        HalfEdge edge = mesh.GetHalfEdgeData(halfEdge).topology;
        return !mesh.IsFaceAlive(edge.AdjacentFace)
            || !mesh.IsFaceAlive(mesh.GetHalfEdgeData(edge.TwinHalfEdge).topology.AdjacentFace);
    }

    private static bool IsOutgoingFromSameVertex(SpatialMesh mesh, int source, int candidate)
    {
        if (!mesh.IsHalfEdgeAlive(candidate))
            return false;

        return mesh.GetHalfEdgeData(candidate).topology.SourceVertex
            == mesh.GetHalfEdgeData(source).topology.SourceVertex;
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
