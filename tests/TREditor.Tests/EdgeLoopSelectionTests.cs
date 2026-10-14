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

    private static readonly Vector2[] TriangleUvs =
    [
        Vector2.Zero,
        Vector2.UnitX,
        Vector2.UnitY,
    ];

    [Fact]
    public void CollectTraversesEvenOppositesAndStopsAtTheBoundary()
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
    public void CollectFollowsTheLowerHandleFaceThroughOddValenceVertices()
    {
        using SpatialMesh mesh = SpatialMeshBuilder.CreateAabb(Vector3.Zero, Vector3.One);
        int start = mesh.GetFaceData(0).topology.AdjacentHalfEdge;
        int seed = Canonical(mesh, start);

        Assert.Equal(FaceEdges(mesh, 0).Order(), EdgeLoopSelection.Collect(mesh, seed).Order());
    }

    [Fact]
    public void CollectCompletesTheMarkedFaceAcrossABoundaryEdge()
    {
        using SpatialMesh mesh = SpatialMeshBuilder.CreateAabb(Vector3.Zero, Vector3.One);
        int[] expected = FaceEdges(mesh, 0);
        HalfEdge first = mesh.GetHalfEdgeData(expected[0]).topology;
        HalfEdge firstTwin = mesh.GetHalfEdgeData(first.TwinHalfEdge).topology;
        int adjacentFace = first.AdjacentFace == 0 ? firstTwin.AdjacentFace : first.AdjacentFace;
        mesh.DeleteFaces([adjacentFace]);
        int seed = expected[2];
        int twin = mesh.GetHalfEdgeData(seed).topology.TwinHalfEdge;

        IReadOnlyList<int> loop = EdgeLoopSelection.Collect(mesh, seed);

        Assert.Equal(expected.Order(), loop.Order());
        Assert.Equal(loop.Order(), EdgeLoopSelection.Collect(mesh, twin).Order());
    }

    [Fact]
    public void CollectFollowsBoundaryThroughAnEvenValenceVertex()
    {
        using var mesh = new SpatialMesh();
        int center = mesh.AddVertex(Vector3.Zero);
        int a = mesh.AddVertex(new Vector3(1.0f, 0.0f, 0.0f));
        int b = mesh.AddVertex(new Vector3(0.5f, 1.0f, 0.0f));
        int c = mesh.AddVertex(new Vector3(-0.5f, 1.0f, 0.0f));
        int d = mesh.AddVertex(new Vector3(-1.0f, 0.0f, 0.0f));
        mesh.AddFace([center, a, b], TriangleUvs);
        mesh.AddFace([center, b, c], TriangleUvs);
        mesh.AddFace([center, c, d], TriangleUvs);

        int[] expected =
        [
            FindEdge(mesh, center, a),
            FindEdge(mesh, a, b),
            FindEdge(mesh, b, c),
            FindEdge(mesh, c, d),
            FindEdge(mesh, d, center),
        ];

        Assert.Equal(
            expected.Order(),
            EdgeLoopSelection.Collect(mesh, FindEdge(mesh, center, a)).Order()
        );
    }

    [Fact]
    public void CollectUsesTheOppositeEdgeAtHigherEvenValence()
    {
        const int Sides = 6;
        using var mesh = new SpatialMesh();
        int center = mesh.AddVertex(Vector3.Zero);
        var ring = new int[Sides];
        for (int i = 0; i < Sides; i++)
        {
            float angle = MathF.Tau * i / Sides;
            ring[i] = mesh.AddVertex(new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0.0f));
        }
        for (int i = 0; i < Sides; i++)
            mesh.AddFace([center, ring[i], ring[(i + 1) % Sides]], TriangleUvs);

        IReadOnlyList<int> loop = EdgeLoopSelection.Collect(
            mesh,
            FindOutgoing(mesh, center, ring[0])
        );

        Assert.Contains(FindEdge(mesh, center, ring[Sides / 2]), loop);
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

    private static int[] FaceEdges(SpatialMesh mesh, int face)
    {
        var edges = new List<int>();
        int start = mesh.GetFaceData(face).topology.AdjacentHalfEdge;
        int current = start;
        do
        {
            edges.Add(Canonical(mesh, current));
            current = mesh.GetHalfEdgeData(current).topology.NextHalfEdge;
        } while (current != start);

        return [.. edges];
    }

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
