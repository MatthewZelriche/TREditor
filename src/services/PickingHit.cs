using Godot;

public readonly record struct PickingHit(
    MeshObjectId ObjectId,
    Vector3 Position,
    Vector3 Normal
);
