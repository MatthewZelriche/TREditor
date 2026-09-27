using System;

// A selection is either an object, one of its mesh components, or empty. Keeping this as one value
// prevents object-only systems (notably transform gizmos) from mistaking a component selection for
// an object selection.
public readonly record struct SelectionValue
{
    private SelectionValue(MeshObjectId? objectId, MeshComponentId? component)
    {
        ObjectId = objectId;
        Component = component;
    }

    public MeshObjectId? ObjectId { get; }

    public MeshComponentId? Component { get; }

    public bool IsEmpty => ObjectId is null && Component is null;

    public static SelectionValue Empty => default;

    public static SelectionValue ForObject(MeshObjectId objectId) => new(objectId, null);

    public static SelectionValue ForComponent(MeshComponentId component)
    {
        if (component.Slot < 0)
            throw new ArgumentOutOfRangeException(nameof(component), "A component slot cannot be negative.");

        return new SelectionValue(null, component);
    }
}
