using Godot;

namespace TREditor2026.Tests;

public sealed class TubeMeshBuilderTests
{
    [Fact]
    public void Append_AddsExpectedOpenTubeGeometry()
    {
        List<Vector3> vertices = [];
        List<Vector3> normals = [];
        List<int> indices = [];

        Assert.True(
            TubeMeshBuilder.Append(Vector3.Zero, Vector3.Up, 0.5f, 8, vertices, normals, indices)
        );

        Assert.Equal(16, vertices.Count);
        Assert.Equal(16, normals.Count);
        Assert.Equal(48, indices.Count);
        Assert.All(normals, normal => Assert.InRange(normal.Length(), 0.99999f, 1.00001f));
    }

    [Fact]
    public void Append_ReverseWindingReversesEveryTriangle()
    {
        List<Vector3> vertices = [];
        List<Vector3> normals = [];
        List<int> forward = [];
        List<int> reverse = [];

        TubeMeshBuilder.Append(Vector3.Zero, Vector3.Right, 1f, 3, vertices, normals, forward);
        vertices.Clear();
        normals.Clear();
        TubeMeshBuilder.Append(
            Vector3.Zero,
            Vector3.Right,
            1f,
            3,
            vertices,
            normals,
            reverse,
            reverseWinding: true
        );

        for (int index = 0; index < forward.Count; index += 3)
        {
            Assert.Equal(forward[index], reverse[index]);
            Assert.Equal(forward[index + 1], reverse[index + 2]);
            Assert.Equal(forward[index + 2], reverse[index + 1]);
        }
    }

    [Fact]
    public void Append_ZeroLengthSegmentDoesNotChangeBuffers()
    {
        List<Vector3> vertices = [Vector3.One];
        List<Vector3> normals = [Vector3.Up];
        List<int> indices = [0];

        Assert.False(
            TubeMeshBuilder.Append(Vector3.Zero, Vector3.Zero, 1f, 8, vertices, normals, indices)
        );

        Assert.Single(vertices);
        Assert.Single(normals);
        Assert.Single(indices);
    }
}
