using Godot;

public readonly record struct ComponentPickingHit(
    MeshComponentId Component,
    Vector3 Position,
    float Distance
);
