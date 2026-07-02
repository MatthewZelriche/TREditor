using System;
using System.Collections.Generic;
using Godot;

public static class EditorDocumentValidator
{
    public static void Validate(EditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ValidateMappings(document.MaterialMappings);
        ValidateObjects(document.Objects);
    }

    private static void ValidateMappings(IReadOnlyList<MaterialSlotMapping> mappings)
    {
        HashSet<int> slots = [];
        HashSet<string> assetIds = new(StringComparer.Ordinal);
        foreach (MaterialSlotMapping mapping in mappings)
        {
            if (mapping.Slot <= 0)
                throw new FormatException($"Material slot {mapping.Slot} must be positive.");
            if (!slots.Add(mapping.Slot))
                throw new FormatException($"Material slot {mapping.Slot} appears more than once.");

            string normalized;
            try
            {
                normalized = TextureMaterialLibrary.NormalizeAssetId(mapping.AssetId);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException(
                    $"Material slot {mapping.Slot} has an invalid asset ID.",
                    exception
                );
            }

            if (!string.Equals(mapping.AssetId, normalized, StringComparison.Ordinal))
                throw new FormatException(
                    $"Texture asset ID '{mapping.AssetId}' is not normalized as '{normalized}'."
                );
            if (!assetIds.Add(normalized))
                throw new FormatException(
                    $"Texture asset ID '{normalized}' appears more than once."
                );
        }
    }

    // Note that this could get very expensive for large documents.
    private static void ValidateObjects(IReadOnlyList<EditorDocumentObject> objects)
    {
        HashSet<EditorObjectId> objectIds = [];
        foreach (EditorDocumentObject documentObject in objects)
        {
            if (documentObject.Id.Value == Guid.Empty)
                throw new FormatException("Document object IDs must not be empty.");
            if (!objectIds.Add(documentObject.Id))
                throw new FormatException(
                    $"Document object ID '{documentObject.Id}' appears more than once."
                );
            if (!IsFinite(documentObject.Transform))
                throw new FormatException(
                    $"Document object '{documentObject.Id}' has a non-finite transform."
                );

            try
            {
                documentObject.Mesh.ValidateConsistency();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                throw new FormatException(
                    $"Document object '{documentObject.Id}' has invalid mesh topology.",
                    exception
                );
            }
        }
    }

    private static bool IsFinite(Transform3D transform) =>
        IsFinite(transform.Basis.Column0)
        && IsFinite(transform.Basis.Column1)
        && IsFinite(transform.Basis.Column2)
        && IsFinite(transform.Origin);

    private static bool IsFinite(Vector3 vector) =>
        float.IsFinite(vector.X) && float.IsFinite(vector.Y) && float.IsFinite(vector.Z);
}
