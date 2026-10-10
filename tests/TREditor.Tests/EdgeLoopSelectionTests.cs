using System.Numerics;
using TRMesh.Mesh;

public sealed class EdgeLoopSelectionTests
{
    private static readonly Vector2[] QuadUvs =
    [
        Vector2.Zero,
        Vector2.UnitX,
        Vector2.One,
        Vector2.UnitY,
    ];

    [Fact]
    public void CollectTraversesBothDirectionsThroughFourValenceVertices()
    {
        using SpatialMesh mesh = CreateGrid(out int[,] vertices);
        int seed = FindEdge(mesh, vertices[1, 1], vertices[2, 1]);

        IReadOnlyList<int> loop = EdgeLoopSelection.Collect(mesh, seed);

        int[] expected =
        [
            FindEdge(mesh, vertices[0, 1], vertices[1, 1]),
            FindEdge(mesh, vertices[1, 1], vertices[2, 1]),
            FindEdge(mesh, vertices[2, 1], vertices[3, 1]),
        ];
        Assert.Equal(expected.Order(), loop.Order());
    }

    [Fact]
    public void CollectStopsAtSingularVertices()
    {
        using SpatialMesh mesh = SpatialMeshBuilder.CreateAabb(Vector3.Zero, Vector3.One);
        int seed = Canonical(mesh, mesh.GetFaceData(0).topology.AdjacentHalfEdge);

        Assert.Equal([seed], EdgeLoopSelection.Collect(mesh, seed));
    }

    [Fact]
    public void CollectStopsWhenAClosedLoopReturnsToItsSeed()
    {
        const int Sides = 6;
        using SpatialMesh mesh = CreateTube(Sides, out int[,] vertices);
        int seed = FindEdge(mesh, vertices[0, 1], vertices[1, 1]);

        IReadOnlyList<int> loop = EdgeLoopSelection.Collect(mesh, seed);

        Assert.Equal(Sides, loop.Count);
        Assert.Equal(Sides, loop.Distinct().Count());
    }

    [Fact]
    public void CollectCanonicalizesEitherHalfEdgeAndRejectsDeadHandles()
    {
        using SpatialMesh mesh = CreateGrid(out int[,] vertices);
        int seed = FindOutgoing(mesh, vertices[1, 1], vertices[2, 1]);
        int twin = mesh.GetHalfEdgeData(seed).topology.TwinHalfEdge;

        Assert.Equal(
            EdgeLoopSelection.Collect(mesh, seed).Order(),
            EdgeLoopSelection.Collect(mesh, twin).Order()
        );
        Assert.Empty(EdgeLoopSelection.Collect(mesh, SpatialMesh.INVALID_HANDLE));
    }

    private static SpatialMesh CreateGrid(out int[,] vertices)
    {
        var mesh = new SpatialMesh();
        vertices = new int[4, 3];
        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 4; x++)
                vertices[x, y] = mesh.AddVertex(new Vector3(x, y, 0.0f));
        }

        for (int y = 0; y < 2; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                mesh.AddFace(
                    [
                        vertices[x, y],
                        vertices[x + 1, y],
                        vertices[x + 1, y + 1],
                        vertices[x, y + 1],
                    ],
                    QuadUvs
                );
            }
        }

        return mesh;
    }

    private static SpatialMesh CreateTube(int sides, out int[,] vertices)
    {
        var mesh = new SpatialMesh();
        vertices = new int[sides, 3];
        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < sides; x++)
            {
                float angle = MathF.Tau * x / sides;
                vertices[x, y] = mesh.AddVertex(
                    new Vector3(MathF.Cos(angle), y, MathF.Sin(angle))
                );
            }
        }

        for (int y = 0; y < 2; y++)
        {
            for (int x = 0; x < sides; x++)
            {
                int next = (x + 1) % sides;
                mesh.AddFace(
                    [
                        vertices[x, y],
                        vertices[next, y],
                        vertices[next, y + 1],
                        vertices[x, y + 1],
                    ],
                    QuadUvs
                );
            }
        }

        return mesh;
    }

    private static int FindEdge(SpatialMesh mesh, int from, int to) =>
        Canonical(mesh, FindOutgoing(mesh, from, to));

    private static int FindOutgoing(SpatialMesh mesh, int from, int to)
    {
        int start = mesh.GetVertexData(from).topology.OutgoingHalfEdge;
        int halfEdge = start;
        do
        {
            int twin = mesh.GetHalfEdgeData(halfEdge).topology.TwinHalfEdge;
            if (mesh.GetHalfEdgeData(twin).topology.SourceVertex == to)
                return halfEdge;
            halfEdge = mesh.GetHalfEdgeData(twin).topology.NextHalfEdge;
        } while (halfEdge != start);

        throw new InvalidOperationException($"No edge connects vertices {from} and {to}.");
    }

    private static int Canonical(SpatialMesh mesh, int halfEdge) =>
        Math.Min(halfEdge, mesh.GetHalfEdgeData(halfEdge).topology.TwinHalfEdge);
}
