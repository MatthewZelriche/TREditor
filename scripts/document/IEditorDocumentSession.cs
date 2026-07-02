using System;
using System.Collections.Generic;

internal interface IEditorDocumentSession
{
    void CancelPreview();

    EditorDocument CaptureDocument();

    void Reset();

    void Apply(LoadedEditorDocument document);
}

internal sealed class EditorDocumentSession : IEditorDocumentSession
{
    private readonly EditorSceneService _scene;
    private readonly TextureMaterialLibrary _textureMaterials;
    private readonly SelectionService _selection;
    private readonly CommandService _commands;
    private readonly Action _cancelPreview;

    public EditorDocumentSession(
        EditorSceneService scene,
        TextureMaterialLibrary textureMaterials,
        SelectionService selection,
        CommandService commands,
        Action cancelPreview
    )
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(textureMaterials);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(cancelPreview);

        _scene = scene;
        _textureMaterials = textureMaterials;
        _selection = selection;
        _commands = commands;
        _cancelPreview = cancelPreview;
    }

    public void CancelPreview() => _cancelPreview();

    public EditorDocument CaptureDocument()
    {
        List<EditorDocumentObject> objects = [];
        foreach ((EditorObjectId id, TRMeshGD meshNode) in _scene.EnumerateMeshObjects())
        {
            objects.Add(
                new EditorDocumentObject(
                    id,
                    meshNode.Name.ToString(),
                    meshNode.Transform,
                    meshNode.SourceMesh
                )
            );
        }

        return new EditorDocument(objects, _textureMaterials.GetMappings());
    }

    // Clear history before freeing meshes so command-owned topology patches release reservations.
    public void Reset()
    {
        _commands.ClearHistory();
        _selection.Apply(SelectionSnapshot.Empty);
        _scene.ClearAll();
        _textureMaterials.Clear();
    }

    public void Apply(LoadedEditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        foreach (MaterialSlotMapping mapping in document.MaterialMappings)
            _textureMaterials.RegisterSlot(mapping.Slot, mapping.AssetId);

        foreach (EditorDocumentObject documentObject in document.Objects)
        {
            if (
                !_scene.CreateMeshObject(
                    documentObject.Id,
                    documentObject.Mesh,
                    documentObject.Name,
                    documentObject.Transform
                )
            )
            {
                throw new InvalidOperationException(
                    $"Could not create loaded object '{documentObject.Id}'."
                );
            }

            document.TransferMeshOwnership(documentObject);
        }
    }
}
