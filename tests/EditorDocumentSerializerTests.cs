using Godot;
using TREditorSharp;
using TREditorSharp.Builders;
using NumericsVector2 = System.Numerics.Vector2;
using NumericsVector3 = System.Numerics.Vector3;

namespace TREditor2026.Tests;

public sealed class EditorDocumentSerializerTests
{
    [Fact]
    public void RoundTrip_PreservesObjectMetadataMaterialTableAndGeometry()
    {
        SpatialMesh mesh = MeshBuilders.Build(
            new BlockOptions
            {
                Min = new NumericsVector3(-1f, -2f, -3f),
                Max = new NumericsVector3(1f, 2f, 3f),
            }
        );

        const int slot = 7;
        var expectedUv = new NumericsVector2(0.25f, 0.75f);
        FaceHandle texturedFace = FirstFace(mesh);
        mesh.SetFaceMaterialSlot(texturedFace, slot);
        mesh.SetFaceUvsInitialized(texturedFace, true);
        foreach (HalfEdgeHandle corner in mesh.HalfEdgesAroundFace(texturedFace))
        {
            mesh.SetFaceCornerUv(corner, expectedUv);
        }

        var id = new EditorObjectId(Guid.Parse("11111111-2222-3333-4444-555555555555"));
        var transform = new Transform3D(
            new Basis(Vector3.Up, 0.5f).Scaled(new Vector3(1f, 2f, 3f)),
            new Vector3(10f, 20f, 30f)
        );
        MaterialSlotMapping[] mappings = [new MaterialSlotMapping(slot, "walls/brick.png")];
        var document = new EditorDocument(
            [new EditorDocumentObject(id, "Box", transform, mesh)],
            mappings
        );

        var serializer = new EditorDocumentSerializer();
        using var stream = new MemoryStream();
        serializer.Write(document, stream);
        stream.Position = 0;
        EditorDocument reloaded = serializer.Read(stream);

        Assert.Single(reloaded.Objects);
        EditorDocumentObject reloadedObject = reloaded.Objects[0];
        Assert.Equal(id, reloadedObject.Id);
        Assert.Equal("Box", reloadedObject.Name);
        AssertTransformsEqual(transform, reloadedObject.Transform);
        Assert.Equal(mappings, reloaded.MaterialMappings.ToArray());

        // Full mesh fidelity is covered by TRMesh's own binary tests; here we spot-check that the
        // embedded blob is intact and that the textured face's slot and UVs survived.
        Assert.Equal(SortedPositions(mesh), SortedPositions(reloadedObject.Mesh));
        FaceHandle reloadedTexturedFace = SingleFaceWithSlot(reloadedObject.Mesh, slot);
        foreach (
            HalfEdgeHandle corner in reloadedObject.Mesh.HalfEdgesAroundFace(reloadedTexturedFace)
        )
        {
            Assert.Equal(expectedUv, reloadedObject.Mesh.GetFaceCornerUv(corner));
        }
    }

    [Fact]
    public void RoundTrip_EmptyDocument()
    {
        var document = new EditorDocument([], []);
        var serializer = new EditorDocumentSerializer();

        using var stream = new MemoryStream();
        serializer.Write(document, stream);
        stream.Position = 0;
        EditorDocument reloaded = serializer.Read(stream);

        Assert.Empty(reloaded.Objects);
        Assert.Empty(reloaded.MaterialMappings);
    }

    [Fact]
    public void Read_RejectsDataThatIsNotATredDocument()
    {
        var serializer = new EditorDocumentSerializer();
        using var stream = new MemoryStream([1, 2, 3, 4, 5, 6, 7, 8]);

        Assert.Throws<FormatException>(() => serializer.Read(stream));
    }

    private static void AssertTransformsEqual(Transform3D expected, Transform3D actual)
    {
        Assert.Equal(expected.Basis.Column0, actual.Basis.Column0);
        Assert.Equal(expected.Basis.Column1, actual.Basis.Column1);
        Assert.Equal(expected.Basis.Column2, actual.Basis.Column2);
        Assert.Equal(expected.Origin, actual.Origin);
    }

    private static FaceHandle FirstFace(SpatialMesh mesh)
    {
        foreach (FaceHandle face in mesh.EnumerateLiveFaces())
        {
            return face;
        }

        throw new InvalidOperationException("Mesh has no live faces.");
    }

    private static FaceHandle SingleFaceWithSlot(SpatialMesh mesh, int slot)
    {
        FaceHandle match = default;
        int count = 0;
        foreach (FaceHandle face in mesh.EnumerateLiveFaces())
        {
            if (mesh.GetFaceMaterialSlot(face) == slot)
            {
                match = face;
                count++;
            }
        }

        Assert.Equal(1, count);
        return match;
    }

    private static List<NumericsVector3> SortedPositions(SpatialMesh mesh)
    {
        List<NumericsVector3> positions = [];
        foreach (VertexHandle vertex in mesh.EnumerateLiveVertices())
        {
            positions.Add(mesh.GetVertexPosition(vertex));
        }

        positions.Sort(
            (left, right) =>
            {
                int compareX = left.X.CompareTo(right.X);
                if (compareX != 0)
                {
                    return compareX;
                }

                int compareY = left.Y.CompareTo(right.Y);
                return compareY != 0 ? compareY : left.Z.CompareTo(right.Z);
            }
        );
        return positions;
    }
}
