using Godot;
using TREditorSharp;

namespace TREditor2026.Tests;

public sealed class SelectionHighlightControllerTests
{
    [Fact]
    public void ObjectHighlight_QueriesViewForSelectedModelObjects()
    {
        EditorObjectId selectedId = new(Guid.NewGuid());
        EditorObjectId otherId = new(Guid.NewGuid());
        EditorSceneModel model = new();
        model.Add(
            new EditorObjectModel(selectedId, "Selected", Transform3D.Identity, new SpatialMesh())
        );
        model.Add(new EditorObjectModel(otherId, "Other", Transform3D.Identity, new SpatialMesh()));

        TrackingSceneView view = new();
        SelectionService selection = new();
        using ObjectSelectionHighlightController controller = new(model, view, selection);

        controller.SetActive(true);
        selection.Apply(SelectionSnapshot.From([SelectionTarget.ForObject(selectedId)]));

        Assert.Contains(selectedId, view.NodeLookupIds);
        Assert.DoesNotContain(otherId, view.NodeLookupIds);
    }

    private sealed class TrackingSceneView : IEditorSceneView
    {
        public List<EditorObjectId> NodeLookupIds { get; } = [];

        public bool Attach(EditorObjectModel obj) => true;

        public void Destroy(EditorObjectId id) { }

        public void SyncTransform(EditorObjectModel obj) { }

        public void SyncGeometry(EditorObjectId id) { }

        public void SyncRender(EditorObjectId id) { }

        public bool TryGetNode(EditorObjectId id, out TRMeshGD node)
        {
            NodeLookupIds.Add(id);
            node = null!;
            return false;
        }

        public IEnumerable<KeyValuePair<EditorObjectId, TRMeshGD>> Nodes => [];

        public void Clear() { }
    }
}
