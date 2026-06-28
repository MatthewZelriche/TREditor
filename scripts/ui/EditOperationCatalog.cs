using System;
using System.Collections.Generic;

public enum EditOperationAvailability
{
    Available,
    CoreReady,
    Planned,
}

public readonly record struct EditOperationDefinition(
    string Id,
    string DisplayName,
    string Description,
    string Selection,
    string Input,
    EditOperationAvailability Availability
);

public static class EditOperationCatalog
{
    private static readonly EditOperationDefinition[] Operations =
    [
        new(
            "ExtrudeFace",
            "Extrude Face",
            "Push or pull a face to add depth and create new side surfaces.",
            "One face",
            "Select this tool, or Shift + drag the move handle",
            EditOperationAvailability.Available
        ),
        new(
            "DeleteSelection",
            "Delete Selection",
            "Remove the selected vertices, edges, or faces from the mesh.",
            "Any vertices, edges, or faces",
            "Delete",
            EditOperationAvailability.Available
        ),
        new(
            "EdgeCut",
            "Edge Cut",
            "Add a new edge across a face.",
            "A vertex, edge, or face",
            "Coming soon",
            EditOperationAvailability.CoreReady
        ),
        new(
            "CollapseVertices",
            "Collapse Vertices",
            "Merge connected vertices into one vertex.",
            "Two or more connected vertices on one object",
            "Choose a target for two vertices; larger selections use the centroid",
            EditOperationAvailability.Available
        ),
        new(
            "CollapseFace",
            "Collapse Face",
            "Merge every vertex of a face into one vertex at its centroid.",
            "Exactly one face",
            "Preview, then Apply or press Enter",
            EditOperationAvailability.Available
        ),
        new(
            "InsetFace",
            "Inset Face",
            "Shrink a face inward, leaving a smaller face inside a raised border.",
            "Exactly one face",
            "Adjust depth, then Apply or press Enter",
            EditOperationAvailability.Available
        ),
        new(
            "BevelEdge",
            "Bevel Edge",
            "Chamfer sharp edges by replacing them with a sloped strip.",
            "Non-touching solid edges at simple three-edge corners",
            "Adjust width, then Apply or press Enter",
            EditOperationAvailability.Available
        ),
        new(
            "BevelVertex",
            "Bevel Vertex",
            "Chamfer corners by replacing each selected vertex with a cap face.",
            "One or more non-adjacent solid vertices",
            "Adjust width, then Apply or press Enter",
            EditOperationAvailability.Available
        ),
        new(
            "FillHole",
            "Fill Hole",
            "Cover a gap in the mesh with a new face.",
            "One edge on an open boundary",
            "Preview, then Apply or press Enter",
            EditOperationAvailability.Available
        ),
        new(
            "BridgeEdges",
            "Bridge Edges",
            "Connect two boundary edges with a flat or arched strip of quads.",
            "Exactly two boundary edges on one object",
            "Adjust segments and arch angle, then Apply or press Enter",
            EditOperationAvailability.Available
        ),
        new(
            "DetachFace",
            "Detach Face",
            "Split selected faces into a disconnected region within the same mesh.",
            "One or more faces",
            "Preview, then Apply or press Enter",
            EditOperationAvailability.Available
        ),
    ];

    public static IReadOnlyList<EditOperationDefinition> All => Operations;

    public static EditOperationDefinition Get(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        foreach (EditOperationDefinition operation in Operations)
        {
            if (operation.Id == id)
                return operation;
        }

        throw new ArgumentException($"Unknown edit operation '{id}'.", nameof(id));
    }
}
