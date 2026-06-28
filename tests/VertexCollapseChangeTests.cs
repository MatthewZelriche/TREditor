using System.Numerics;
using TREditorSharp;

namespace TREditor2026.Tests;

public sealed class VertexCollapseChangeTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );

    [Theory]
    [InlineData(CollapseVerticesTarget.First)]
    [InlineData(CollapseVerticesTarget.Second)]
    public void Collapse_TwoVerticesPreservesChosenTarget(CollapseVerticesTarget target)
    {
        using SpatialMesh mesh = BuildQuad(out VertexHandle first, out VertexHandle second, out _);
        VertexHandle expected = target == CollapseVerticesTarget.First ? first : second;
        Vector3 expectedPosition = mesh.GetVertexPosition(expected);

        using VertexCollapseChange change = AssertCollapse(mesh, [first, second], target);

        Assert.Equal(expected, change.Survivor);
        Assert.True(mesh.IsVertexAlive(expected));
        Assert.Equal(expectedPosition, mesh.GetVertexPosition(expected));
    }

    [Fact]
    public void Collapse_MoreThanTwoVerticesMovesSurvivorToCentroid()
    {
        using SpatialMesh mesh = BuildQuad(
            out VertexHandle first,
            out VertexHandle second,
            out VertexHandle third
        );
        Vector3 centroid =
            (
                mesh.GetVertexPosition(first)
                + mesh.GetVertexPosition(second)
                + mesh.GetVertexPosition(third)
            ) / 3f;

        using VertexCollapseChange change = AssertCollapse(
            mesh,
            [first, second, third],
            CollapseVerticesTarget.Second
        );

        Assert.Equal(first, change.Survivor);
        Assert.Equal(centroid, mesh.GetVertexPosition(change.Survivor));
    }

    [Fact]
    public void Collapse_UndoRedoRestoresExactHandles()
    {
        using SpatialMesh mesh = BuildQuad(out VertexHandle first, out VertexHandle second, out _);
        using VertexCollapseChange change = AssertCollapse(
            mesh,
            [first, second],
            CollapseVerticesTarget.First
        );

        change.ApplyBefore();

        Assert.True(mesh.IsVertexAlive(first));
        Assert.True(mesh.IsVertexAlive(second));

        change.ApplyAfter();

        Assert.True(mesh.IsVertexAlive(first));
        Assert.False(mesh.IsVertexAlive(second));
    }

    [Fact]
    public void CanCollapse_RejectsDisconnectedVerticesWithoutMutation()
    {
        using SpatialMesh mesh = BuildQuad(out VertexHandle first, out _, out _);
        VertexHandle disconnected = mesh.AddVertex(new Vector3(10, 10, 10));

        Assert.False(
            VertexCollapseChange.CanCollapse(
                mesh,
                [first, disconnected],
                CollapseVerticesTarget.First
            )
        );

        Assert.True(mesh.IsVertexAlive(first));
        Assert.True(mesh.IsVertexAlive(disconnected));
    }

    [Fact]
    public void CanCollapse_RestoresMeshAfterTestingOperation()
    {
        using SpatialMesh mesh = BuildQuad(out VertexHandle first, out VertexHandle second, out _);

        Assert.True(
            VertexCollapseChange.CanCollapse(mesh, [first, second], CollapseVerticesTarget.First)
        );

        Assert.True(mesh.IsVertexAlive(first));
        Assert.True(mesh.IsVertexAlive(second));
    }

    private static VertexCollapseChange AssertCollapse(
        SpatialMesh mesh,
        VertexHandle[] vertices,
        CollapseVerticesTarget target
    )
    {
        VertexCollapseChange? change = VertexCollapseChange.Collapse(
            ObjectId,
            mesh,
            vertices,
            target
        );
        Assert.NotNull(change);
        return change;
    }

    private static SpatialMesh BuildQuad(
        out VertexHandle first,
        out VertexHandle second,
        out VertexHandle third
    )
    {
        SpatialMesh mesh = new();
        first = mesh.AddVertex(Vector3.Zero);
        second = mesh.AddVertex(Vector3.UnitX);
        third = mesh.AddVertex(Vector3.One);
        VertexHandle fourth = mesh.AddVertex(Vector3.UnitY);
        mesh.AddFace([first, second, third, fourth]);
        return mesh;
    }
}
