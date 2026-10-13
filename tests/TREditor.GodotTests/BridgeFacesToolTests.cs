using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GdUnit4;
using Godot;
using TRMesh.Mesh;
using static GdUnit4.Assertions;
using NumericsVector2 = System.Numerics.Vector2;
using NumericsVector3 = System.Numerics.Vector3;

namespace TREditor.GodotTests;

[TestSuite]
public class BridgeFacesToolTests
{
    private static readonly Vector2 ViewportSize = new(640.0f, 480.0f);

    [TestCase]
    [RequireGodotRuntime]
    public void BridgesFacesFromSeparateObjectsAsAThirdMeshWithUndoRedo()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        SpatialMesh firstMesh = CreateQuad(0.0f, out int firstFace);
        SpatialMesh secondMesh = CreateQuad(0.0f, out int secondFace);
        MeshObject first = application.CurrentDocument!.Add(
            "First",
            Transform3D.Identity,
            new GDMesh(firstMesh)
        );
        MeshObject second = application.CurrentDocument.Add(
            "Second",
            new Transform3D(Basis.Identity, new Vector3(0.0f, 0.0f, 4.0f)),
            new GDMesh(secondMesh)
        );
        SelectionValue originalSelection = SelectFaces(first, firstFace, second, secondFace);
        application.Selection.Apply(originalSelection);
        application.Tools.Activate(BridgeFacesTool.ToolId);
        var tool = (BridgeFacesTool)application.Tools.ActiveTool!;
        tool.IntermediateSteps = 2;
        long serialBefore = application.CurrentDocument.MutationSerial;

        AssertThat(application.Tools.TryExecutePrimaryAction()).IsNull();

        AssertThat(application.CurrentDocument.ObjectCount).IsEqual(3);
        AssertThat(application.CurrentDocument.MutationSerial).IsEqual(serialBefore + 1);
        AssertThat(firstMesh.FaceCount).IsEqual(0);
        AssertThat(secondMesh.FaceCount).IsEqual(0);
        MeshObject bridge = application.CurrentDocument
            .GetObjects()
            .Single(candidate => candidate.Id != first.Id && candidate.Id != second.Id);
        AssertThat(bridge.Name).IsEqual("Bridge");
        AssertBool(bridge.Transform.Origin.IsEqualApprox(new Vector3(1.0f, 1.0f, 0.0f)))
            .IsTrue();
        AssertThat(bridge.Mesh.SpatialMesh.VertexCount).IsEqual(16);
        AssertThat(bridge.Mesh.SpatialMesh.FaceCount).IsEqual(12);
        AssertThat(application.Selection.Current.Components.Count).IsEqual(12);
        AssertBool(
                application.Selection.Current.Components.All(component =>
                    component.ObjectId == bridge.Id && component.Type == MeshComponentType.Face
                )
            )
            .IsTrue();
        AssertBool(application.BezierCurveGizmo.IsVisible).IsFalse();
        AssertThat(application.ToolContext.Preview.Geometry).IsNull();

        AssertBool(application.Undo()).IsTrue();

        AssertThat(application.CurrentDocument.ObjectCount).IsEqual(2);
        AssertThat(application.CurrentDocument.MutationSerial).IsEqual(serialBefore + 2);
        AssertThat(firstMesh.FaceCount).IsEqual(1);
        AssertThat(secondMesh.FaceCount).IsEqual(1);
        AssertBool(application.Selection.Current == originalSelection).IsTrue();

        AssertBool(application.Redo()).IsTrue();

        AssertThat(application.CurrentDocument.ObjectCount).IsEqual(3);
        AssertThat(application.CurrentDocument.MutationSerial).IsEqual(serialBefore + 3);
        AssertThat(firstMesh.FaceCount).IsEqual(0);
        AssertThat(secondMesh.FaceCount).IsEqual(0);
        AssertThat(application.Selection.Current.Components.Count).IsEqual(12);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void BridgesTwoDisconnectedFacesFromOneObject()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        SpatialMesh mesh = CreateDisconnectedQuads(out int firstFace, out int secondFace);
        MeshObject source = application.CurrentDocument!.Add(
            "Source",
            Transform3D.Identity,
            new GDMesh(mesh)
        );
        application.Selection.Apply(SelectFaces(source, firstFace, source, secondFace));
        application.Tools.Activate(BridgeFacesTool.ToolId);
        ((BridgeFacesTool)application.Tools.ActiveTool!).IntermediateSteps = 0;

        AssertThat(application.Tools.TryExecutePrimaryAction()).IsNull();

        AssertThat(application.CurrentDocument.ObjectCount).IsEqual(2);
        AssertThat(mesh.FaceCount).IsEqual(0);
        MeshObject bridge = application.CurrentDocument
            .GetObjects()
            .Single(candidate => candidate.Id != source.Id);
        AssertThat(bridge.Mesh.SpatialMesh.VertexCount).IsEqual(8);
        AssertThat(bridge.Mesh.SpatialMesh.FaceCount).IsEqual(4);

        AssertBool(application.Undo()).IsTrue();
        AssertThat(application.CurrentDocument.ObjectCount).IsEqual(1);
        AssertThat(mesh.FaceCount).IsEqual(2);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void UsesQuadraticGizmoAndLiveThirdMeshPreview()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        SpatialMesh firstMesh = CreateQuad(0.0f, out int firstFace);
        SpatialMesh secondMesh = CreateQuad(4.0f, out int secondFace);
        MeshObject first = application.CurrentDocument!.Add(
            "First",
            Transform3D.Identity,
            new GDMesh(firstMesh)
        );
        MeshObject second = application.CurrentDocument.Add(
            "Second",
            Transform3D.Identity,
            new GDMesh(secondMesh)
        );
        application.Selection.Apply(SelectFaces(first, firstFace, second, secondFace));
        application.Tools.Activate(BridgeFacesTool.ToolId);
        var tool = (BridgeFacesTool)application.Tools.ActiveTool!;

        EditorToolAction action = tool.CreatePrimaryAction(application.ToolContext);

        AssertThat(action.Command).IsNotNull();
        AssertBool(application.BezierCurveGizmo.IsVisible).IsTrue();
        AssertThat(tool.Curve).IsNotNull();
        AssertThat(tool.Curve!.PointCount).IsEqual(3);
        AssertBool(
                application.BezierCurveGizmo.IsHandleLocked(
                    new BezierCurveHandle(0, BezierCurveHandleKind.Anchor)
                )
            )
            .IsTrue();
        AssertBool(
                application.BezierCurveGizmo.IsHandleLocked(
                    new BezierCurveHandle(1, BezierCurveHandleKind.Anchor)
                )
            )
            .IsFalse();
        AssertBool(
                application.BezierCurveGizmo.IsHandleLocked(
                    new BezierCurveHandle(2, BezierCurveHandleKind.Anchor)
                )
            )
            .IsTrue();
        GeometryPreview preview = application.ToolContext.Preview.Geometry!;
        AssertThat(preview.Mesh.SpatialMesh.VertexCount).IsEqual(20);
        AssertThat(preview.Mesh.SpatialMesh.FaceCount).IsEqual(16);
        AssertBool(preview.Transform == tool.Plan!.BridgeTransform).IsTrue();

        tool.Cancel(application.ToolContext);

        AssertBool(application.BezierCurveGizmo.IsVisible).IsFalse();
        AssertThat(application.ToolContext.Preview.Geometry).IsNull();
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task SelectedControlPointUsesWorldAlignedTranslationGizmo()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        AddToTree(mapRoot);
        using var application = new Application(mapRoot);
        application.Snap.SetGridSnap(null);
        SpatialMesh firstMesh = CreateQuad(0.0f, out int firstFace);
        SpatialMesh secondMesh = CreateQuad(4.0f, out int secondFace);
        MeshObject first = application.CurrentDocument!.Add(
            "First",
            Transform3D.Identity,
            new GDMesh(firstMesh)
        );
        MeshObject second = application.CurrentDocument.Add(
            "Second",
            Transform3D.Identity,
            new GDMesh(secondMesh)
        );
        application.Selection.Apply(SelectFaces(first, firstFace, second, secondFace));
        application.Tools.Activate(BridgeFacesTool.ToolId);
        var tool = (BridgeFacesTool)application.Tools.ActiveTool!;
        AssertThat(tool.CreatePrimaryAction(application.ToolContext).Command).IsNotNull();
        Vector3 originalWorld =
            tool.Plan!.BridgeTransform * tool.Curve!.GetPointPosition(1);
        MainViewport viewport = AutoFree(
            new MainViewport { Position = Vector2.Zero, Size = ViewportSize }
        );
        AddToTree(viewport);
        viewport.Camera.LookAtFromPosition(
            originalWorld + new Vector3(7.0f, 6.0f, 8.0f),
            originalWorld,
            Vector3.Up
        );
        application.RegisterToolViewport(viewport.Camera, viewport.ToolOverlayRoot);
        await ProcessFrames(3);

        Vector2 controlPoint = viewport.Camera.UnprojectPosition(originalWorld);
        application.HandleToolMouseDown(
            new EditorMouseInput(viewport.Camera, controlPoint)
        );

        AssertThat(application.BezierCurveGizmo.SelectedHandle)
            .IsEqual(new BezierCurveHandle(1, BezierCurveHandleKind.Anchor));
        AssertBool(application.BezierCurveGizmo.IsInteracting).IsFalse();
        AssertBool(application.Gizmos.IsTransientTranslation).IsTrue();
        AssertThat(application.Gizmos.ProxyTransform.Origin).IsEqual(originalWorld);
        AssertBool(application.Gizmos.ProxyTransform.Basis.IsEqualApprox(Basis.Identity))
            .IsTrue();
        await ProcessFrames(2);

        Vector2 handle = FindWorldXAxisHandle(
            application.Gizmos,
            viewport.Camera,
            originalWorld
        );
        Vector2 screenAxis = (
            viewport.Camera.UnprojectPosition(originalWorld + Vector3.Right)
            - controlPoint
        ).Normalized();
        application.HandleToolMouseDown(new EditorMouseInput(viewport.Camera, handle));
        application.HandleToolMouseDrag(
            new EditorMouseInput(viewport.Camera, handle + (screenAxis * 60.0f))
        );
        application.HandleToolMouseUp(
            new EditorMouseInput(viewport.Camera, handle + (screenAxis * 60.0f))
        );

        Vector3 movedWorld = tool.Plan.BridgeTransform * tool.Curve.GetPointPosition(1);
        AssertBool(Mathf.IsEqualApprox(movedWorld.X, originalWorld.X)).IsFalse();
        AssertFloat(movedWorld.Y).IsEqualApprox(originalWorld.Y, 0.001f);
        AssertFloat(movedWorld.Z).IsEqualApprox(originalWorld.Z, 0.001f);

        tool.Cancel(application.ToolContext);
        AssertBool(application.Gizmos.IsVisible).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void RejectsSharedTopologyAndUnequalEdgeCounts()
    {
        Node3D mapRoot = AutoFree(new Node3D());
        using var application = new Application(mapRoot);
        SpatialMesh adjacent = CreateAdjacentQuads(out int left, out int right);
        MeshObject source = application.CurrentDocument!.Add(
            "Adjacent",
            Transform3D.Identity,
            new GDMesh(adjacent)
        );
        application.Selection.Apply(SelectFaces(source, left, source, right));
        application.Tools.Activate(BridgeFacesTool.ToolId);

        EditorToolAction sharedAction =
            ((BridgeFacesTool)application.Tools.ActiveTool!).CreatePrimaryAction(
                application.ToolContext
            );

        AssertThat(sharedAction.Command).IsNull();
        AssertThat(sharedAction.UnavailableReason).Contains("share a vertex or edge");

        SpatialMesh triangle = CreateTriangle(out int triangleFace);
        MeshObject triangleObject = application.CurrentDocument.Add(
            "Triangle",
            new Transform3D(Basis.Identity, Vector3.Forward * 4.0f),
            new GDMesh(triangle)
        );
        application.Selection.Apply(SelectFaces(source, left, triangleObject, triangleFace));

        EditorToolAction unequalAction =
            ((BridgeFacesTool)application.Tools.ActiveTool!).CreatePrimaryAction(
                application.ToolContext
            );

        AssertThat(unequalAction.Command).IsNull();
        AssertThat(unequalAction.UnavailableReason).Contains("same number of edges");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void StripUvsUseOneContinuousRectangularIsland()
    {
        Vector3[][] rings =
        [
            [new(-1, -1, 0), new(1, -1, 0), new(1, 1, 0), new(-1, 1, 0)],
            [new(-1, -1, 2), new(1, -1, 2), new(1, 1, 2), new(-1, 1, 2)],
        ];
        using SpatialMesh mesh = BridgeFacesGeometry.BuildMesh(rings, out int[] faces);

        AssertThat(faces.Length).IsEqual(4);
        var uvs = new List<NumericsVector2>();
        int start = mesh.GetFaceData(faces[^1]).topology.AdjacentHalfEdge;
        int current = start;
        do
        {
            uvs.Add(mesh.GetHalfEdgeData(current).UV);
            current = mesh.GetHalfEdgeData(current).topology.NextHalfEdge;
        } while (current != start);

        AssertBool(uvs.Any(uv => uv.X == 1.0f)).IsTrue();
        AssertBool(uvs.Any(uv => uv.Y == 0.0f)).IsTrue();
        AssertBool(uvs.Any(uv => uv.Y == 1.0f)).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void OrientedCrossSectionFramesPreserveThicknessThroughNinetyDegreeTurn()
    {
        using var document = new Document();
        SpatialMesh firstMesh = CreateFace(
            [
                new(0, -1, -1),
                new(0, 1, -1),
                new(0, 1, 1),
                new(0, -1, 1),
            ],
            out int firstFace
        );
        SpatialMesh secondMesh = CreateFace(
            [
                new(3, -1, 4),
                new(5, -1, 4),
                new(5, 1, 4),
                new(3, 1, 4),
            ],
            out int secondFace
        );
        MeshObject first = document.Add(
            "First",
            Transform3D.Identity,
            new GDMesh(firstMesh)
        );
        MeshObject second = document.Add(
            "Second",
            Transform3D.Identity,
            new GDMesh(secondMesh)
        );
        BridgeFacesPlan plan = BridgeFacesGeometry.Resolve(
            document,
            SelectFaces(first, firstFace, second, secondFace)
        );
        using Curve3D curve = BridgeFacesGeometry.CreateQuadraticCurve(plan);
        curve.SetPointPosition(1, curve.GetPointPosition(1) + Vector3.Up * 3.0f);
        BridgeFacesGeometry.SynchronizeQuadraticTangents(curve);

        Vector3[][] rings = BridgeFacesGeometry.CreateRings(plan, curve, 6);

        foreach (Vector3[] ring in rings)
        {
            for (int corner = 0; corner < ring.Length; corner++)
            {
                AssertFloat(ring[corner].DistanceTo(ring[(corner + 1) % ring.Length]))
                    .IsEqualApprox(2.0f, 0.001f);
            }
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void FacingFacesUseOppositeSourceWindingWithoutTwistingCorrespondence()
    {
        using var document = new Document();
        SpatialMesh firstMesh = CreateFace(
            [
                new(-1, -1, 0),
                new(1, -1, 0),
                new(1, 1, 0),
                new(-1, 1, 0),
            ],
            out int firstFace
        );
        SpatialMesh secondMesh = CreateFace(
            [
                new(-1, -1, 4),
                new(-1, 1, 4),
                new(1, 1, 4),
                new(1, -1, 4),
            ],
            out int secondFace
        );
        MeshObject first = document.Add(
            "First",
            Transform3D.Identity,
            new GDMesh(firstMesh)
        );
        MeshObject second = document.Add(
            "Second",
            Transform3D.Identity,
            new GDMesh(secondMesh)
        );

        BridgeFacesPlan plan = BridgeFacesGeometry.Resolve(
            document,
            SelectFaces(first, firstFace, second, secondFace)
        );

        for (int corner = 0; corner < plan.First.WorldPositions.Length; corner++)
        {
            Vector3 firstPosition = plan.First.WorldPositions[corner];
            Vector3 secondPosition = plan.MatchedSecondPositions[corner];
            AssertFloat(firstPosition.X).IsEqualApprox(secondPosition.X, 0.001f);
            AssertFloat(firstPosition.Y).IsEqualApprox(secondPosition.Y, 0.001f);
        }
    }

    private static SelectionValue SelectFaces(
        MeshObject first,
        int firstFace,
        MeshObject second,
        int secondFace
    ) =>
        SelectionValue.ForSelections(
            [],
            [
                new(first.Id, MeshComponentType.Face, firstFace),
                new(second.Id, MeshComponentType.Face, secondFace),
            ]
        );

    private static Vector2 FindWorldXAxisHandle(
        TranslateGizmoCoordinator coordinator,
        Camera3D camera,
        Vector3 origin
    )
    {
        Vector2 center = camera.UnprojectPosition(origin);
        Vector2 axis = (camera.UnprojectPosition(origin + Vector3.Right) - center).Normalized();
        Vector2 found = Vector2.Zero;
        for (int index = 1; index <= 60; index++)
        {
            Vector2 candidate = center + (axis * (4.0f * index));
            if (coordinator.BeginInteraction(camera, candidate))
            {
                found = candidate;
                coordinator.Cancel();
            }
        }

        AssertBool(found != Vector2.Zero).IsTrue();
        return found;
    }

    private static void AddToTree(Node node) =>
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(node);

    private static async Task ProcessFrames(int count)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        for (int index = 0; index < count; index++)
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    private static SpatialMesh CreateQuad(float z, out int face)
    {
        var mesh = new SpatialMesh();
        int a = mesh.AddVertex(new NumericsVector3(0, 0, z));
        int b = mesh.AddVertex(new NumericsVector3(2, 0, z));
        int c = mesh.AddVertex(new NumericsVector3(2, 2, z));
        int d = mesh.AddVertex(new NumericsVector3(0, 2, z));
        face = mesh.AddFace(
            [a, b, c, d],
            [NumericsVector2.Zero, NumericsVector2.UnitX, NumericsVector2.One, NumericsVector2.UnitY]
        );
        return mesh;
    }

    private static SpatialMesh CreateDisconnectedQuads(out int firstFace, out int secondFace)
    {
        SpatialMesh mesh = CreateQuad(0.0f, out firstFace);
        int a = mesh.AddVertex(new NumericsVector3(0, 0, 4));
        int b = mesh.AddVertex(new NumericsVector3(2, 0, 4));
        int c = mesh.AddVertex(new NumericsVector3(2, 2, 4));
        int d = mesh.AddVertex(new NumericsVector3(0, 2, 4));
        secondFace = mesh.AddFace(
            [a, b, c, d],
            [NumericsVector2.Zero, NumericsVector2.UnitX, NumericsVector2.One, NumericsVector2.UnitY]
        );
        return mesh;
    }

    private static SpatialMesh CreateAdjacentQuads(out int left, out int right)
    {
        var mesh = new SpatialMesh();
        int a = mesh.AddVertex(new NumericsVector3(-1, 0, 0));
        int b = mesh.AddVertex(new NumericsVector3(0, 0, 0));
        int c = mesh.AddVertex(new NumericsVector3(0, 1, 0));
        int d = mesh.AddVertex(new NumericsVector3(-1, 1, 0));
        int e = mesh.AddVertex(new NumericsVector3(1, 0, 0));
        int f = mesh.AddVertex(new NumericsVector3(1, 1, 0));
        NumericsVector2[] uvs =
            [NumericsVector2.Zero, NumericsVector2.UnitX, NumericsVector2.One, NumericsVector2.UnitY];
        left = mesh.AddFace([a, b, c, d], uvs);
        right = mesh.AddFace([b, e, f, c], uvs);
        return mesh;
    }

    private static SpatialMesh CreateTriangle(out int face)
    {
        var mesh = new SpatialMesh();
        int a = mesh.AddVertex(NumericsVector3.Zero);
        int b = mesh.AddVertex(NumericsVector3.UnitX);
        int c = mesh.AddVertex(NumericsVector3.UnitY);
        face = mesh.AddFace(
            [a, b, c],
            [NumericsVector2.Zero, NumericsVector2.UnitX, NumericsVector2.UnitY]
        );
        return mesh;
    }

    private static SpatialMesh CreateFace(NumericsVector3[] positions, out int face)
    {
        var mesh = new SpatialMesh();
        var vertices = new int[positions.Length];
        var uvs = new NumericsVector2[positions.Length];
        for (int index = 0; index < positions.Length; index++)
        {
            vertices[index] = mesh.AddVertex(positions[index]);
            uvs[index] = new NumericsVector2(index % 2, index / 2);
        }
        face = mesh.AddFace(vertices, uvs);
        return mesh;
    }
}
