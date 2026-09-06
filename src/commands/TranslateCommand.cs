using System;
using Godot;

// A committed move of one object. Both the before and after transforms are absolute rather than a
// delta, so undo and redo land on exactly the same places no matter how many times they run or what
// else touched the object in between.
public sealed class TranslateCommand : IEditorCommand
{
    private readonly MeshObjectId _id;
    private readonly Transform3D _before;
    private readonly Transform3D _after;

    public TranslateCommand(MeshObjectId id, Transform3D before, Transform3D after)
    {
        _id = id;
        _before = before;
        _after = after;
    }

    public string DisplayName => "Translate";

    public void Execute(Document document) => Apply(document, _after);

    public void Undo(Document document) => Apply(document, _before);

    private void Apply(Document document, Transform3D transform)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!document.SetTransform(_id, transform))
        {
            throw new InvalidOperationException(
                $"Object {_id.Value} no longer exists and cannot be translated."
            );
        }
    }
}
