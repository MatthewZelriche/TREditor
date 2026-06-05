using System;

public sealed class EditorCommandContext
{
    public EditorCommandContext(EditorSceneService scene, SelectionService selection)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(selection);

        Scene = scene;
        Selection = selection;
    }

    public EditorSceneService Scene { get; }

    public SelectionService Selection { get; }
}
