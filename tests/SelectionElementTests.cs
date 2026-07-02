namespace TREditor2026.Tests;

public sealed class SelectionElementTests
{
    [Fact]
    public void None_IsInvalid()
    {
        SelectionElement element = SelectionElement.None;

        Assert.False(element.IsValid);
        Assert.Equal(ScenePickElementKind.None, element.Kind);
    }

    [Fact]
    public void Object_ExposesOnlyObjectKind()
    {
        SelectionElement element = SelectionElement.Object();

        Assert.True(element.IsValid);
        Assert.Equal(ScenePickElementKind.Object, element.Kind);
        Assert.False(element.TryGetVertex(out _));
        Assert.False(element.TryGetEdge(out _));
        Assert.False(element.TryGetFace(out _));
    }

    [Fact]
    public void Vertex_ExposesOnlyVertexHandle()
    {
        VertexHandle vertex = new(2, 1);
        SelectionElement element = SelectionElement.Vertex(vertex);

        Assert.Equal(ScenePickElementKind.Vertex, element.Kind);
        Assert.True(element.TryGetVertex(out VertexHandle actual));
        Assert.Equal(vertex, actual);
        Assert.False(element.TryGetEdge(out _));
        Assert.False(element.TryGetFace(out _));
    }

    [Fact]
    public void Edge_ExposesOnlyEdgeHandle()
    {
        HalfEdgeHandle edge = new(3, 2);
        SelectionElement element = SelectionElement.Edge(edge);

        Assert.Equal(ScenePickElementKind.Edge, element.Kind);
        Assert.True(element.TryGetEdge(out HalfEdgeHandle actual));
        Assert.Equal(edge, actual);
        Assert.False(element.TryGetVertex(out _));
        Assert.False(element.TryGetFace(out _));
    }

    [Fact]
    public void Face_ExposesOnlyFaceHandle()
    {
        FaceHandle face = new(4, 0);
        SelectionElement element = SelectionElement.Face(face);

        Assert.Equal(ScenePickElementKind.Face, element.Kind);
        Assert.True(element.TryGetFace(out FaceHandle actual));
        Assert.Equal(face, actual);
        Assert.False(element.TryGetVertex(out _));
        Assert.False(element.TryGetEdge(out _));
    }
}
