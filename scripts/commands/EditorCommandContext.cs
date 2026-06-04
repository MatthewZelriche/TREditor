using System;

public sealed class EditorCommandContext
{
    public EditorCommandContext(EditorSceneService scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        Scene = scene;
    }

    public EditorSceneService Scene { get; }
}
