using System.Numerics;
using TRMesh.Mesh;

namespace TREditor.Tests;

public class SpatialMeshSerializationTests
{
    [Fact]
    public void SerializeDeserialize_AabbPreservesTopologyCounts()
    {
        using SpatialMesh source = SpatialMeshBuilder.CreateAabb(Vector3.Zero, Vector3.One);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        source.Serialize(writer);
        writer.Flush();

        stream.Position = 0;
        using var reader = new BinaryReader(stream);
        using SpatialMesh loaded = SpatialMesh.Deserialize(reader);

        Assert.Equal(source.VertexCount, loaded.VertexCount);
        Assert.Equal(source.HalfEdgeCount, loaded.HalfEdgeCount);
        Assert.Equal(source.FaceCount, loaded.FaceCount);
    }
}
