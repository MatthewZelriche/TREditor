using System;
using System.Collections.Generic;
using TREditorSharp;

/// <summary>
/// Finds the unique topological boundary edges of polygon faces that cannot be triangulated.
/// </summary>
public static class InvalidFaceEdgeCollector
{
    public static void Collect(
        SpatialMesh mesh,
        List<HalfEdgeHandle> output,
        List<FaceCornerHandle> triangulationScratch
    )
    {
        ArgumentNullException.ThrowIfNull(mesh);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(triangulationScratch);

        output.Clear();
        HashSet<int> seenEdgeIndices = [];

        foreach (FaceHandle face in mesh.EnumerateLiveFaces())
        {
            triangulationScratch.Clear();
            if (mesh.TriangulateFace(face, triangulationScratch))
                continue;

            foreach (HalfEdgeHandle edge in mesh.HalfEdgesAroundFace(face))
            {
                HalfEdgeHandle canonical = mesh.GetCanonicalEdge(edge);
                if (seenEdgeIndices.Add(canonical.Index))
                    output.Add(canonical);
            }
        }

        triangulationScratch.Clear();
    }
}
