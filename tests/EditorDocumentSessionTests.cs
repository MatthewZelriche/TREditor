using System.Linq;
using Godot;
using TREditorSharp;
using TREditorSharp.Builders;
using NumericsVector3 = System.Numerics.Vector3;

namespace TREditor2026.Tests;

public sealed class EditorDocumentSessionTests
{
    private static readonly EditorObjectId FirstId = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );
    private static readonly EditorObjectId SecondId = new(
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")
    );

    [Fact]
    public void CaptureDocument_ExcludesCommandOwnedObjects()
    {
        TestSession session = CreateSession();
        session.Scene.CreateMeshObject(FirstId, new SpatialMesh(), "Live");
        session.Scene.CreateMeshObject(SecondId, new SpatialMesh(), "Deleted");
        DeleteMeshCommand delete = DeleteMeshCommand.CreateIfAny(
            SelectionSnapshot.From([SelectionTarget.ForObject(SecondId)])
        )!;
        session.Commands.Execute(delete);

        EditorDocument captured = session.DocumentSession.CaptureDocument();

        Assert.Single(captured.Objects);
        Assert.Equal(FirstId, captured.Objects[0].Id);
    }

    [Fact]
    public void CaptureDocument_ReadsModelTransformEvenWhenViewIsStale()
    {
        TestSession session = CreateSession();
        session.Scene.CreateMeshObject(FirstId, new SpatialMesh(), "Box");
        Assert.True(session.Model.TryGet(FirstId, out EditorObjectModel obj));

        Transform3D modelTransform = new(
            new Basis(Vector3.Up, 0.75f).Scaled(new Vector3(2f, 3f, 4f)),
            new Vector3(5f, 6f, 7f)
        );
        obj.SetLocalTransform(modelTransform);

        EditorDocument captured = session.DocumentSession.CaptureDocument();

        Assert.Single(captured.Objects);
        AssertTransformsEqual(modelTransform, captured.Objects[0].Transform);
    }

    [Fact]
    public void CaptureDocument_SortsObjectsById()
    {
        TestSession session = CreateSession();
        session.Scene.CreateMeshObject(SecondId, new SpatialMesh(), "Second");
        session.Scene.CreateMeshObject(FirstId, new SpatialMesh(), "First");

        EditorDocument captured = session.DocumentSession.CaptureDocument();

        Assert.Equal(2, captured.Objects.Count);
        Assert.Equal(FirstId, captured.Objects[0].Id);
        Assert.Equal(SecondId, captured.Objects[1].Id);
    }

    [Fact]
    public void CaptureDocument_RepeatedCapturesProduceIdenticalSerializedBytes()
    {
        TestSession session = CreateSession();
        session.Scene.CreateMeshObject(SecondId, BuildBox(), "Second");
        session.Scene.CreateMeshObject(FirstId, BuildBox(), "First");
        session.Materials.RegisterSlot(2, "floors/metal.png");
        session.Materials.RegisterSlot(1, "walls/brick.png");

        byte[] first = Serialize(session.DocumentSession.CaptureDocument());
        byte[] second = Serialize(session.DocumentSession.CaptureDocument());

        Assert.Equal(first, second);
    }

    [Fact]
    public void Apply_RoundTripsIdentityTransformNameGeometryAndMaterials()
    {
        TestSession session = CreateSession();
        SpatialMesh mesh = BuildBox();
        Transform3D transform = new(
            new Basis(Vector3.Up, 0.5f).Scaled(new Vector3(1f, 2f, 3f)),
            new Vector3(10f, 20f, 30f)
        );
        session.Scene.CreateMeshObject(FirstId, mesh, "Box", transform);
        session.Materials.RegisterSlot(1, "walls/brick.png");

        List<NumericsVector3> expectedPositions = SortedPositions(mesh);
        EditorDocument captured = session.DocumentSession.CaptureDocument();
        byte[] bytes = Serialize(captured);

        session.DocumentSession.Reset();
        using LoadedEditorDocument loaded = new EditorDocumentSerializer().Read(
            new MemoryStream(bytes)
        );
        session.DocumentSession.Apply(loaded);

        Assert.True(session.Model.TryGet(FirstId, out EditorObjectModel obj));
        Assert.Equal("Box", obj.Name);
        AssertTransformsEqual(transform, obj.LocalTransform);
        Assert.Equal(expectedPositions, SortedPositions(obj.Mesh));
        MaterialSlotMapping[] expectedMappings = [new MaterialSlotMapping(1, "walls/brick.png")];
        Assert.Equal(expectedMappings, session.Materials.GetMappings().ToArray());
    }

    [Fact]
    public void Apply_FailedLifecycleAddDisposesTakenObjectWithoutLeavingLoadedOwnership()
    {
        TestSession session = CreateSession(allowAttach: false);
        SpatialMesh mesh = new();
        EditorDocument document = new(
            [new EditorDocumentObject(FirstId, "Box", Transform3D.Identity, mesh)],
            []
        );
        using LoadedEditorDocument loaded = new(document);

        Assert.Throws<InvalidOperationException>(() => session.DocumentSession.Apply(loaded));
        Assert.Empty(session.Model.Objects);
        AssertMeshDisposed(mesh);
    }

    [Fact]
    public void Reset_ClearsPreviewHistorySelectionSceneAndMaterials()
    {
        TestSession session = CreateSession();
        bool previewCancelled = false;
        session.DocumentSession = new EditorDocumentSession(
            session.Scene,
            session.Materials,
            session.Selection,
            session.Commands,
            () => previewCancelled = true
        );

        session.Scene.CreateMeshObject(FirstId, new SpatialMesh(), "Box");
        session.Materials.RegisterSlot(1, "walls/brick.png");
        session.Selection.Apply(SelectionSnapshot.From([SelectionTarget.ForObject(FirstId)]));

        session.DocumentSession.Reset();

        Assert.True(previewCancelled);
        Assert.Empty(session.Model.Objects);
        Assert.Empty(session.Materials.GetMappings());
        Assert.True(session.Selection.Current.IsEmpty);
        Assert.False(session.Commands.CanUndo);
    }

    private static TestSession CreateSession(bool allowAttach = true)
    {
        TestSession session = new();
        session.View.AllowAttach = allowAttach;
        return session;
    }

    private static SpatialMesh BuildBox()
    {
        SpatialMesh mesh = MeshBuilders.Build(
            new BlockOptions
            {
                Min = new NumericsVector3(-1f, -1f, -1f),
                Max = new NumericsVector3(1f, 1f, 1f),
            }
        );
        FaceHandle face = default;
        foreach (FaceHandle candidate in mesh.EnumerateLiveFaces())
        {
            face = candidate;
            break;
        }
        mesh.SetFaceMaterialSlot(face, 1);
        mesh.SetFaceUvsInitialized(face, true);
        foreach (HalfEdgeHandle corner in mesh.HalfEdgesAroundFace(face))
            mesh.SetFaceCornerUv(corner, new System.Numerics.Vector2(0.25f, 0.75f));
        return mesh;
    }

    private static byte[] Serialize(EditorDocument document)
    {
        using MemoryStream stream = new();
        new EditorDocumentSerializer().Write(document, stream);
        return stream.ToArray();
    }

    private static void AssertTransformsEqual(Transform3D expected, Transform3D actual)
    {
        Assert.Equal(expected.Basis.Column0, actual.Basis.Column0);
        Assert.Equal(expected.Basis.Column1, actual.Basis.Column1);
        Assert.Equal(expected.Basis.Column2, actual.Basis.Column2);
        Assert.Equal(expected.Origin, actual.Origin);
    }

    private static List<NumericsVector3> SortedPositions(SpatialMesh mesh)
    {
        List<NumericsVector3> positions = [];
        foreach (VertexHandle vertex in mesh.EnumerateLiveVertices())
            positions.Add(mesh.GetVertexPosition(vertex));
        positions.Sort(static (left, right) => left.X.CompareTo(right.X));
        return positions;
    }

    private static void AssertMeshDisposed(SpatialMesh mesh) =>
        Assert.Throws<ObjectDisposedException>(
            () => mesh.AddVertex(new NumericsVector3(0f, 0f, 0f))
        );

    private sealed class TestSession
    {
        public EditorSceneModel Model { get; }
        public FakeEditorSceneView View { get; }
        public EditorSceneService Scene { get; }
        public TextureMaterialLibrary Materials { get; } = new();
        public SelectionService Selection { get; } = new();
        public EditorObjectLifecycle Lifecycle { get; }
        public CommandService Commands { get; }
        public EditorDocumentSession DocumentSession { get; set; }

        public TestSession()
        {
            Model = new EditorSceneModel();
            View = new FakeEditorSceneView();
            Lifecycle = new EditorObjectLifecycle(Model, View);
            EditorMeshOperations operations = new(Model, View);
            Scene = new EditorSceneService(Lifecycle, Model, View, operations);
            Commands = new CommandService(
                new EditorCommandContext(Lifecycle, operations, Selection)
            );
            DocumentSession = new EditorDocumentSession(
                Scene,
                Materials,
                Selection,
                Commands,
                () => { }
            );
        }
    }

    private sealed class FakeEditorSceneView : IEditorSceneView
    {
        public bool AllowAttach { get; set; } = true;

        public bool Attach(EditorObjectModel obj) => AllowAttach;

        public void Destroy(EditorObjectId id) { }

        public void SyncTransform(EditorObjectModel obj) { }

        public void SyncGeometry(EditorObjectId id) { }

        public void SyncRender(EditorObjectId id) { }

        public bool TryGetNode(EditorObjectId id, out TRMeshGD node)
        {
            node = null!;
            return false;
        }

        public IEnumerable<KeyValuePair<EditorObjectId, TRMeshGD>> Nodes => [];

        public void Clear() { }
    }
}
