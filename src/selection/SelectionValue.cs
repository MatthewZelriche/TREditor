using System;
using System.Collections.Generic;
using System.Collections.Immutable;

// An immutable snapshot of every active object and component selection. Selection commands retain
// these values so undo and redo can restore the complete mixed selection exactly.
public readonly struct SelectionValue : IEquatable<SelectionValue>
{
    private static readonly ImmutableHashSet<MeshObjectId> NoObjects =
        [];
    private static readonly ImmutableHashSet<MeshComponentId> NoComponents =
        [];

    private readonly ImmutableHashSet<MeshObjectId>? _objectIds;
    private readonly ImmutableHashSet<MeshComponentId>? _components;

    private SelectionValue(
        ImmutableHashSet<MeshObjectId> objectIds,
        ImmutableHashSet<MeshComponentId> components
    )
    {
        _objectIds = objectIds;
        _components = components;
    }

    public IReadOnlySet<MeshObjectId> ObjectIds => ObjectSet;

    public IReadOnlySet<MeshComponentId> Components => ComponentSet;

    public bool IsEmpty => ObjectSet.Count == 0 && ComponentSet.Count == 0;

    public static SelectionValue Empty => default;

    public static SelectionValue ForObject(MeshObjectId objectId) =>
        new(NoObjects.Add(objectId), NoComponents);

    public static SelectionValue ForComponent(MeshComponentId component)
    {
        Validate(component);
        return new SelectionValue(NoObjects, NoComponents.Add(component));
    }

    public static SelectionValue ForSelections(
        IEnumerable<MeshObjectId> objectIds,
        IEnumerable<MeshComponentId> components
    )
    {
        ArgumentNullException.ThrowIfNull(objectIds);
        ArgumentNullException.ThrowIfNull(components);

        ImmutableHashSet<MeshObjectId> objects = [.. objectIds];
        ImmutableHashSet<MeshComponentId> meshComponents = [.. components];
        foreach (MeshComponentId component in meshComponents)
            Validate(component);

        return objects.Count == 0 && meshComponents.Count == 0
            ? Empty
            : new SelectionValue(objects, meshComponents);
    }

    public SelectionValue Toggle(MeshObjectId objectId)
    {
        ImmutableHashSet<MeshObjectId> objects = ObjectSet.Contains(objectId)
            ? ObjectSet.Remove(objectId)
            : ObjectSet.Add(objectId);
        return Create(objects, ComponentSet);
    }

    public SelectionValue Toggle(MeshComponentId component)
    {
        Validate(component);
        ImmutableHashSet<MeshComponentId> components = ComponentSet.Contains(component)
            ? ComponentSet.Remove(component)
            : ComponentSet.Add(component);
        return Create(ObjectSet, components);
    }

    public bool Equals(SelectionValue other) =>
        ObjectSet.SetEquals(other.ObjectSet) && ComponentSet.SetEquals(other.ComponentSet);

    public override bool Equals(object? obj) => obj is SelectionValue other && Equals(other);

    public override int GetHashCode()
    {
        int objectHash = 0;
        foreach (MeshObjectId id in ObjectSet)
            objectHash ^= id.GetHashCode();

        int componentHash = 0;
        foreach (MeshComponentId component in ComponentSet)
            componentHash ^= component.GetHashCode();

        return HashCode.Combine(ObjectSet.Count, objectHash, ComponentSet.Count, componentHash);
    }

    public static bool operator ==(SelectionValue left, SelectionValue right) => left.Equals(right);

    public static bool operator !=(SelectionValue left, SelectionValue right) => !left.Equals(right);

    private ImmutableHashSet<MeshObjectId> ObjectSet => _objectIds ?? NoObjects;

    private ImmutableHashSet<MeshComponentId> ComponentSet => _components ?? NoComponents;

    private static SelectionValue Create(
        ImmutableHashSet<MeshObjectId> objectIds,
        ImmutableHashSet<MeshComponentId> components
    ) => objectIds.Count == 0 && components.Count == 0
        ? Empty
        : new SelectionValue(objectIds, components);

    private static void Validate(MeshComponentId component)
    {
        if (component.Slot < 0)
            throw new ArgumentOutOfRangeException(nameof(component), "A component slot cannot be negative.");
    }
}
