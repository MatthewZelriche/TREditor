using System;
using System.Linq;
using System.Threading.Tasks;
using GdUnit4;
using Godot;
using TRMesh.Mesh;
using static GdUnit4.Assertions;
using NumericsVector3 = System.Numerics.Vector3;

namespace TREditor.GodotTests;

[TestSuite]
public class PrimitiveToolTests
{
    private static readonly Vector2 ViewportSize = new(640.0f, 480.0f);

    [TestCase]
    [RequireGodotRuntime]
    public async Task DraggingBaseThenHeightAndClickingCommitsAnUndoableBox()
    {
        using Fixture fixture = await Fixture.Create();
        fixture.Application.Snap.SetGridSnap(null);
        PrimitiveTool tool = fixture.Tool;
        Vector2 start = fixture.Camera.UnprojectPosition(new Vector3(4.0f, 0.0f, 2.0f));
        Vector2 end = fixture.Camera.UnprojectPosition(new Vector3(6.0f, 0.0f, 4.0f));

        fixture.MouseDown(start);
        fixture.MouseDrag(end);

        AssertBool(tool.IsDefiningBase).IsTrue();
        AssertThat(fixture.Application.Document!.ObjectCount).IsEqual(0);
        AssertThat(fixture.Preview.Geometry).IsNotNull();

        fixture.MouseUp(end);
        AssertBool(tool.IsDefiningHeight).IsTrue();

        fixture.MouseMove(end + new Vector2(0.0f, -60.0f));
        GeometryPreview? raisedPreview = fixture.Preview.Geometry;
        AssertThat(raisedPreview).IsNotNull();

        fixture.MouseDown(end + new Vector2(0.0f, -60.0f));

        AssertBool(tool.IsDefiningBase).IsFalse();
        AssertBool(tool.IsDefiningHeight).IsFalse();
        AssertThat(fixture.Preview.Geometry).IsNull();
        AssertThat(fixture.Application.Document!.ObjectCount).IsEqual(1);
        AssertThat(fixture.Application.UndoDisplayName).IsEqual("Create Primitive");

        MeshObject created = fixture.Application.CurrentDocument!.GetObjects().Single();
        AssertThat(created.Name).IsEqual("Box");
        AssertThat(created.Mesh.SpatialMesh.VertexCount).IsEqual(8);
        AssertThat(created.Mesh.SpatialMesh.FaceCount).IsEqual(6);
        AssertBool(Mathf.IsEqualApprox(created.Transform.Origin.Y, 0.0f)).IsTrue();

        AssertBool(fixture.Application.Undo()).IsTrue();
        AssertThat(fixture.Application.Document.ObjectCount).IsEqual(0);
        AssertBool(fixture.Application.Redo()).IsTrue();
        AssertThat(fixture.Application.Document.ObjectCount).IsEqual(1);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task GridSnapLocksBaseCornersAndExtrusionHeight()
    {
        using Fixture fixture = await Fixture.Create();
        fixture.Application.Snap.SetGridSnap(2.0f);
        // 1 and 5 snap to 2 and 6 with a grid of 2, giving a 4x4 footprint centered at (4,0,4).
        Vector2 start = fixture.Camera.UnprojectPosition(new Vector3(1.0f, 0.0f, 1.0f));
        Vector2 end = fixture.Camera.UnprojectPosition(new Vector3(5.0f, 0.0f, 5.0f));

        fixture.MouseDown(start);
        fixture.MouseDrag(end);
        fixture.MouseUp(end);
        // 55px * 0.02 = 1.1 world units, which snaps to 2.0 with a grid of 2.
        fixture.MouseMove(end + new Vector2(0.0f, -55.0f));
        fixture.MouseDown(end + new Vector2(0.0f, -55.0f));

        MeshObject created = fixture.Application.CurrentDocument!.GetObjects().Single();
        AssertBool(Mathf.IsEqualApprox(created.Transform.Origin.X, 4.0f)).IsTrue();
        AssertBool(Mathf.IsEqualApprox(created.Transform.Origin.Y, 0.0f)).IsTrue();
        AssertBool(Mathf.IsEqualApprox(created.Transform.Origin.Z, 4.0f)).IsTrue();

        NumericsVector3 min = created.Mesh.SpatialMesh.GetVertexData(0).position;
        NumericsVector3 max = created.Mesh.SpatialMesh.GetVertexData(6).position;
        AssertBool(Mathf.IsEqualApprox(min.X, -2.0f)).IsTrue();
        AssertBool(Mathf.IsEqualApprox(min.Y, 0.0f)).IsTrue();
        AssertBool(Mathf.IsEqualApprox(min.Z, -2.0f)).IsTrue();
        AssertBool(Mathf.IsEqualApprox(max.X, 2.0f)).IsTrue();
        AssertBool(Mathf.IsEqualApprox(max.Y, 2.0f)).IsTrue();
        AssertBool(Mathf.IsEqualApprox(max.Z, 2.0f)).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task CylinderSettingsControlTheCommittedTopology()
    {
        using Fixture fixture = await Fixture.Create();
        fixture.Application.Snap.SetGridSnap(null);
        fixture.Tool.Shape = PrimitiveShape.Cylinder;
        fixture.Tool.CylinderSides = 7;
        Vector2 start = fixture.Camera.UnprojectPosition(new Vector3(-4.0f, 0.0f, 1.0f));
        Vector2 end = fixture.Camera.UnprojectPosition(new Vector3(-2.0f, 0.0f, 3.0f));

        fixture.MouseDown(start);
        fixture.MouseDrag(end);
        fixture.MouseUp(end);
        fixture.MouseMove(end + new Vector2(0.0f, -50.0f));
        fixture.MouseDown(end + new Vector2(0.0f, -50.0f));

        MeshObject created = fixture.Application.CurrentDocument!.GetObjects().Single();
        AssertThat(created.Name).IsEqual("Cylinder");
        AssertThat(created.Mesh.SpatialMesh.VertexCount).IsEqual(14);
        AssertThat(created.Mesh.SpatialMesh.FaceCount).IsEqual(9);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task AParallelRayThatMissesGeometryDoesNotBeginCreation()
    {
        using Fixture fixture = await Fixture.Create(
            new Vector3(0.0f, 5.0f, 10.0f),
            new Vector3(0.0f, 5.0f, 0.0f)
        );
        fixture.Application.Snap.SetGridSnap(null);

        fixture.MouseDown(ViewportSize * 0.5f);

        AssertBool(fixture.Tool.IsDefiningBase).IsFalse();
        AssertThat(fixture.Preview.Geometry).IsNull();
        AssertThat(fixture.Application.Document!.ObjectCount).IsEqual(0);
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task AHitSurfaceSuppliesTheBasePointAndNormal()
    {
        using Fixture fixture = await Fixture.Create(
            new Vector3(0.0f, 8.0f, 8.0f),
            new Vector3(0.0f, 0.5f, 0.0f)
        );
        fixture.Application.Snap.SetGridSnap(null);
        fixture.Application.CurrentDocument!.Add(
            "Surface",
            Transform3D.Identity,
            new GDMesh(
                SpatialMeshBuilder.CreateAabb(
                    new NumericsVector3(-2.0f, 0.0f, -2.0f),
                    new NumericsVector3(4.0f, 1.0f, 4.0f)
                )
            )
        );
        fixture.Application.Update(0.0);
        var tree = (SceneTree)Engine.GetMainLoop();
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);

        Vector2 start = fixture.Camera.UnprojectPosition(new Vector3(0.0f, 1.0f, 0.0f));
        AssertBool(
            fixture.Application.ToolContext.Picking.TryRaycast(
                fixture.Camera,
                start,
                out PickingHit hit
            )
        ).IsTrue();
        Vector3 xAxis = Vector3.Right - (hit.Normal * hit.Normal.Dot(Vector3.Right));
        if (xAxis.IsZeroApprox())
            xAxis = Vector3.Forward - (hit.Normal * hit.Normal.Dot(Vector3.Forward));
        xAxis = xAxis.Normalized();
        Vector3 zAxis = xAxis.Cross(hit.Normal).Normalized();
        Vector2 end = fixture.Camera.UnprojectPosition(hit.Position + xAxis + zAxis);

        fixture.MouseDown(start);
        fixture.MouseDrag(end);
        fixture.MouseUp(end);
        fixture.MouseMove(end + new Vector2(0.0f, -40.0f));
        fixture.MouseDown(end + new Vector2(0.0f, -40.0f));

        MeshObject created = fixture.Application.CurrentDocument.GetObjects()
            .Single(value => value.Name == "Box");
        AssertBool(
            Mathf.Abs((created.Transform.Origin - hit.Position).Dot(hit.Normal)) < 0.001f
        ).IsTrue();
        AssertBool(created.Transform.Basis.Y.Dot(hit.Normal) > 0.999f).IsTrue();
    }

    private sealed class Fixture : IDisposable
    {
        private readonly MainViewport _viewport;

        private Fixture(Application application, MainViewport viewport)
        {
            Application = application;
            _viewport = viewport;
        }

        public Application Application { get; }

        public PrimitiveTool Tool => (PrimitiveTool)Application.Tools.ActiveTool!;

        public PreviewState Preview => Application.ToolContext.Preview;

        public Camera3D Camera => _viewport.Camera;

        public static async Task<Fixture> Create(
            Vector3? cameraPosition = null,
            Vector3? cameraTarget = null
        )
        {
            var tree = (SceneTree)Engine.GetMainLoop();
            Node3D mapRoot = AutoFree(new Node3D());
            tree.Root.AddChild(mapRoot);
            var application = new Application(mapRoot);
            application.NewScene();
            MainViewport viewport = AutoFree(
                new MainViewport { Position = Vector2.Zero, Size = ViewportSize }
            );
            tree.Root.AddChild(viewport);
            viewport.Camera.LookAtFromPosition(
                cameraPosition ?? new Vector3(10.0f, 10.0f, 12.0f),
                cameraTarget ?? Vector3.Zero,
                Vector3.Up
            );
            application.RegisterToolViewport(viewport.Camera, viewport.ToolOverlayRoot);
            application.Tools.Activate(PrimitiveTool.ToolId);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            return new Fixture(application, viewport);
        }

        public void MouseDown(Vector2 position) =>
            Application.HandleToolMouseDown(new EditorMouseInput(Camera, position));

        public void MouseDrag(Vector2 position) =>
            Application.HandleToolMouseDrag(new EditorMouseInput(Camera, position));

        public void MouseUp(Vector2 position) =>
            Application.HandleToolMouseUp(new EditorMouseInput(Camera, position));

        public void MouseMove(Vector2 position) =>
            Application.HandleToolMouseMove(new EditorMouseInput(Camera, position));

        public void Dispose() => Application.Dispose();
    }
}
