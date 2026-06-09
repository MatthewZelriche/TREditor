using System;

[Flags]
public enum ComponentHighlightKinds
{
    None = 0,
    Vertices = 1 << 0,
    Edges = 1 << 1,
    Faces = 1 << 2,
    All = Vertices | Edges | Faces,
}

/// <summary>
/// Controls the passive, selected, and hovered components displayed by a component overlay.
/// </summary>
public readonly record struct ComponentHighlightMode(
    ComponentHighlightKinds PassiveKinds,
    ComponentHighlightKinds SelectedKinds,
    ComponentHighlightKinds HoverKinds
)
{
    public static ComponentHighlightMode Edit { get; } =
        EditComponents(ComponentHighlightKinds.All);

    public static ComponentHighlightMode FaceHoverOnly { get; } =
        new(
            ComponentHighlightKinds.None,
            ComponentHighlightKinds.None,
            ComponentHighlightKinds.Faces
        );

    /// <summary>
    /// Creates an Edit-mode policy for a component-selection filter. Faces do not receive a
    /// passive overlay because filling every polygon would obscure the underlying mesh.
    /// </summary>
    public static ComponentHighlightMode EditComponents(ComponentHighlightKinds kinds) =>
        new(
            kinds & (ComponentHighlightKinds.Vertices | ComponentHighlightKinds.Edges),
            kinds,
            kinds
        );

    public bool AllowsSelected(SelectionTarget target) => SelectedKinds.Includes(target.Kind);

    public bool AllowsHover(SelectionTarget target) => HoverKinds.Includes(target.Kind);
}

public static class ComponentHighlightKindsExtensions
{
    public static bool Includes(this ComponentHighlightKinds kinds, ScenePickElementKind kind)
    {
        ComponentHighlightKinds required = kind switch
        {
            ScenePickElementKind.Vertex => ComponentHighlightKinds.Vertices,
            ScenePickElementKind.Edge => ComponentHighlightKinds.Edges,
            ScenePickElementKind.Face => ComponentHighlightKinds.Faces,
            _ => ComponentHighlightKinds.None,
        };
        return required != ComponentHighlightKinds.None && (kinds & required) != 0;
    }
}
