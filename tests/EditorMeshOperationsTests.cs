using Godot;
using TREditorSharp;
using TREditorSharp.Builders;
using NumericsVector3 = System.Numerics.Vector3;

namespace TREditor2026.Tests;

public sealed class EditorMeshOperationsTests
{
    private static readonly EditorObjectId ObjectId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );

    [Fact]
    public void TranslateSelection_ObjectTarget_BumpsOnlyTransformRevision()
    {
        EditorSceneModel model = new();
        TrackingEditorSceneView view = new();
        EditorMeshOperations operations = new(model, view);
        EditorObjectModel obj = new(ObjectId, "Box", Transform3D.Identity, new SpatialMesh());
        model.Add(obj);

        SelectionSnapshot selection = SelectionSnapshot.From([SelectionTarget.ForObject(ObjectId)]);
        Vector3 delta = new(1f, 0f, 0f);

        Assert.True(operations.TranslateSelection(selection, delta));
        Assert.Equal(1ul, obj.TransformRevision);
        Assert.Equal(0ul, obj.GeometryRevision);
        Assert.Equal(0ul, obj.AppearanceRevision);
        Assert.Equal(1, view.TransformSyncCount);
        Assert.Equal(0, view.GeometrySyncCount);
        Assert.Equal(0, view.RenderSyncCount);
    }

    [Fact]
    public void InsetFace_BumpsOnlyGeometryRevisionOnce()
    {
        SpatialMesh mesh = BuildQuad(out FaceHandle face);
        EditorSceneModel model = new();
        TrackingEditorSceneView view = new();
        EditorMeshOperations operations = new(model, view);
        EditorObjectModel obj = new(ObjectId, "Box", Transform3D.Identity, mesh);
        model.Add(obj);

        FaceInsetChange change = operations.InsetFace(
            SelectionTarget.ForFace(ObjectId, face),
            0.25f
        );

        Assert.NotNull(change);
        Assert.Equal(1ul, obj.GeometryRevision);
        Assert.Equal(0ul, obj.AppearanceRevision);
        Assert.Equal(0ul, obj.TransformRevision);
        Assert.Equal(1, view.GeometrySyncCount);
    }

    [Fact]
    public void ApplyFaceTexture_BumpsOnlyAppearanceRevision()
    {
        SpatialMesh mesh = BuildQuad(out FaceHandle face);
        mesh.SetFaceMaterialSlot(face, 1);
        EditorSceneModel model = new();
        TrackingEditorSceneView view = new();
        EditorMeshOperations operations = new(model, view);
        EditorObjectModel obj = new(ObjectId, "Box", Transform3D.Identity, mesh);
        model.Add(obj);

        FaceTextureChange? textureChange = FaceTextureChange.Create(mesh, face, 2);
        Assert.NotNull(textureChange);
        Assert.True(operations.ApplyFaceTexture(ObjectId, textureChange, revert: false));

        Assert.Equal(1ul, obj.AppearanceRevision);
        Assert.Equal(0ul, obj.GeometryRevision);
        Assert.Equal(0ul, obj.TransformRevision);
        Assert.Equal(1, view.RenderSyncCount);
        Assert.Equal(0, view.GeometrySyncCount);
    }

    [Fact]
    public void ApplyFaceInsetBeforeAndAfter_BumpsGeometryRevisionMonotonically()
    {
        SpatialMesh mesh = BuildQuad(out FaceHandle face);
        EditorSceneModel model = new();
        TrackingEditorSceneView view = new();
        EditorMeshOperations operations = new(model, view);
        EditorObjectModel obj = new(ObjectId, "Box", Transform3D.Identity, mesh);
        model.Add(obj);

        FaceInsetChange change = operations.InsetFace(
            SelectionTarget.ForFace(ObjectId, face),
            0.25f
        );
        Assert.NotNull(change);
        Assert.Equal(1ul, obj.GeometryRevision);

        operations.ApplyFaceInsetBefore(change);
        Assert.Equal(2ul, obj.GeometryRevision);

        operations.ApplyFaceInsetAfter(change);
        Assert.Equal(3ul, obj.GeometryRevision);
        Assert.Equal(3, view.GeometrySyncCount);
    }

    [Fact]
    public void BevelEdges_MultiEdgeSelection_BumpsGeometryRevisionOncePerObject()
    {
        SpatialMesh mesh = BuildBox();
        HalfEdgeHandle first = FindEdge(mesh, new(1, -1, 1), new(1, 1, 1));
        HalfEdgeHandle second = FindEdge(mesh, new(-1, -1, -1), new(-1, 1, -1));
        EditorSceneModel model = new();
        TrackingEditorSceneView view = new();
        EditorMeshOperations operations = new(model, view);
        EditorObjectModel obj = new(ObjectId, "Box", Transform3D.Identity, mesh);
        model.Add(obj);

        SelectionTarget[] targets =
        [
            SelectionTarget.ForEdge(ObjectId, first),
            SelectionTarget.ForEdge(ObjectId, second),
        ];

        Assert.NotEmpty(operations.BevelEdges(targets, 0.25f));
        Assert.Equal(1ul, obj.GeometryRevision);
        Assert.Equal(1, view.GeometrySyncCount);
    }

    private static SpatialMesh BuildBox() =>
        MeshBuilders.Build(new BlockOptions { Min = new(-1), Max = new(1) });

    private static HalfEdgeHandle FindEdge(
        SpatialMesh mesh,
        NumericsVector3 origin,
        NumericsVector3 destination
    )
    {
        foreach (HalfEdgeHandle edge in mesh.EnumerateLiveHalfEdges())
        {
            HalfEdge data = mesh.GetHalfEdge(edge);
            if (
                mesh.GetVertexPosition(data.Origin) == origin
                && mesh.GetVertexPosition(mesh.GetHalfEdge(data.Twin).Origin) == destination
            )
            {
                return edge;
            }
        }

        throw new InvalidOperationException("Expected edge was not found.");
    }

    private static SpatialMesh BuildQuad(out FaceHandle face)
    {
        SpatialMesh mesh = new();
        face = mesh.AddFace(
            [
                mesh.AddVertex(NumericsVector3.Zero),
                mesh.AddVertex(NumericsVector3.UnitX),
                mesh.AddVertex(NumericsVector3.UnitX + NumericsVector3.UnitY),
                mesh.AddVertex(NumericsVector3.UnitY),
            ]
        );
        return mesh;
    }

    private sealed class TrackingEditorSceneView : IEditorSceneView
    {
        public int AttachCount { get; private set; }
        public int TransformSyncCount { get; private set; }
        public int GeometrySyncCount { get; private set; }
        public int RenderSyncCount { get; private set; }

        public bool Attach(EditorObjectModel obj)
        {
            AttachCount++;
            return true;
        }

        public void Destroy(EditorObjectId id) { }

        public void SyncTransform(EditorObjectModel obj) => TransformSyncCount++;

        public void SyncGeometry(EditorObjectId id) => GeometrySyncCount++;

        public void SyncRender(EditorObjectId id) => RenderSyncCount++;

        public bool TryGetNode(EditorObjectId id, out TRMeshGD node)
        {
            node = null!;
            return false;
        }

        public IEnumerable<KeyValuePair<EditorObjectId, TRMeshGD>> Nodes => [];

        public void Clear() { }
    }
}
