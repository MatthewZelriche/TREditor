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
    private static readonly ImmutableList<MeshComponentId> NoComponentOrder =
        [];

    private readonly ImmutableHashSet<MeshObjectId>? _objectIds;
    private readonly ImmutableHashSet<MeshComponentId>? _components;
    private readonly ImmutableList<MeshComponentId>? _componentOrder;

    private SelectionValue(
        ImmutableHashSet<MeshObjectId> objectIds,
        ImmutableHashSet<MeshComponentId> components,
        ImmutableList<MeshComponentId> componentOrder
    )
    {
        _objectIds = objectIds;
        _components = components;
        _componentOrder = componentOrder;
    }

    public IReadOnlySet<MeshObjectId> ObjectIds => ObjectSet;

    public IReadOnlySet<MeshComponentId> Components => ComponentSet;

    // Selection order is significant to operations such as merge-at-first/last, while Components
    // remains a set for membership and rendering.
    public IReadOnlyList<MeshComponentId> OrderedComponents => ComponentOrder;

    public bool IsEmpty => ObjectSet.Count == 0 && ComponentSet.Count == 0;

    public static SelectionValue Empty => default;

    public static SelectionValue ForObject(MeshObjectId objectId) =>
        new(NoObjects.Add(objectId), NoComponents, NoComponentOrder);

    public static SelectionValue ForComponent(MeshComponentId component)
    {
        Validate(component);
        return new SelectionValue(
            NoObjects,
            NoComponents.Add(component),
            NoComponentOrder.Add(component)
        );
    }

    public static SelectionValue ForSelections(
        IEnumerable<MeshObjectId> objectIds,
        IEnumerable<MeshComponentId> components
    )
    {
        ArgumentNullException.ThrowIfNull(objectIds);
        ArgumentNullException.ThrowIfNull(components);

        ImmutableHashSet<MeshObjectId> objects = [.. objectIds];
        var meshComponents = NoComponents;
        var componentOrder = NoComponentOrder;
        foreach (MeshComponentId component in components)
        {
            Validate(component);
            if (!meshComponents.Contains(component))
            {
                meshComponents = meshComponents.Add(component);
                componentOrder = componentOrder.Add(component);
            }
        }

        return objects.Count == 0 && meshComponents.Count == 0
            ? Empty
            : new SelectionValue(objects, meshComponents, componentOrder);
    }

    public SelectionValue Toggle(MeshObjectId objectId)
    {
        ImmutableHashSet<MeshObjectId> objects = ObjectSet.Contains(objectId)
            ? ObjectSet.Remove(objectId)
            : ObjectSet.Add(objectId);
        return Create(objects, ComponentSet, ComponentOrder);
    }

    public SelectionValue Toggle(MeshComponentId component)
    {
        Validate(component);
        bool removing = ComponentSet.Contains(component);
        ImmutableHashSet<MeshComponentId> components = removing
            ? ComponentSet.Remove(component)
            : ComponentSet.Add(component);
        ImmutableList<MeshComponentId> order = removing
            ? ComponentOrder.Remove(component)
            : ComponentOrder.Add(component);
        return Create(ObjectSet, components, order);
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

    private ImmutableList<MeshComponentId> ComponentOrder => _componentOrder ?? NoComponentOrder;

    private static SelectionValue Create(
        ImmutableHashSet<MeshObjectId> objectIds,
        ImmutableHashSet<MeshComponentId> components,
        ImmutableList<MeshComponentId> componentOrder
    ) => objectIds.Count == 0 && components.Count == 0
        ? Empty
        : new SelectionValue(objectIds, components, componentOrder);

    private static void Validate(MeshComponentId component)
    {
        if (component.Slot < 0)
            throw new ArgumentOutOfRangeException(nameof(component), "A component slot cannot be negative.");
    }
}
