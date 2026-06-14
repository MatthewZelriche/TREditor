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
            "Merge",
            "Merge",
            "Combine nearby vertices or edges into one.",
            "Vertices or edges that can be combined",
            "Coming soon",
            EditOperationAvailability.CoreReady
        ),
        new(
            "InsetFace",
            "Inset Face",
            "Shrink a face inward, leaving a smaller face inside a raised border.",
            "One or more faces",
            "Coming soon",
            EditOperationAvailability.Planned
        ),
        new(
            "BevelEdge",
            "Bevel Edge",
            "Soften sharp edges by adding a sloped or rounded strip along them.",
            "One or more edges",
            "Coming soon",
            EditOperationAvailability.Planned
        ),
        new(
            "BevelVertex",
            "Bevel Vertex",
            "Soften sharp corners where edges meet.",
            "One or more vertices",
            "Coming soon",
            EditOperationAvailability.Planned
        ),
        new(
            "FillHole",
            "Fill Hole",
            "Cover a gap in the mesh with a new face.",
            "Edges around an open hole",
            "Coming soon",
            EditOperationAvailability.Planned
        ),
        new(
            "BridgeEdges",
            "Bridge Edges",
            "Connect two edge loops with new faces between them.",
            "Two matching edge loops",
            "Coming soon",
            EditOperationAvailability.Planned
        ),
        new(
            "DetachFace",
            "Detach Face",
            "Split selected faces away from the rest of the mesh.",
            "One or more faces",
            "Coming soon",
            EditOperationAvailability.Planned
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
