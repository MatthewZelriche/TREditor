using System;

public readonly record struct EditorObjectId(Guid Value)
{
    public static EditorObjectId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
