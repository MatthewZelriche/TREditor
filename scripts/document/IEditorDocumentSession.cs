using System;
using System.Collections.Generic;
using System.Linq;

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
        List<EditorDocumentObject> objects = _scene
            .Model.Objects.OrderBy(obj => obj.Id.Value)
            .Select(obj => new EditorDocumentObject(obj.Id, obj.Name, obj.LocalTransform, obj.Mesh))
            .ToList();

        return new EditorDocument(objects, _textureMaterials.GetMappings());
    }

    // Cancel preview before clearing history so preview-owned topology patches release reservations.
    public void Reset()
    {
        _cancelPreview();
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
            EditorObjectModel obj = document.TakeObject(documentObject);
            if (!_scene.TryAddObject(obj))
            {
                obj.Dispose();
                throw new InvalidOperationException(
                    $"Could not create loaded object '{documentObject.Id}'."
                );
            }
        }
    }
}
