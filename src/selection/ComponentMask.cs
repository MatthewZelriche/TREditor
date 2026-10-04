using System;

[Flags]
public enum ComponentMask
{
    None = 0,
    Vertex = 1 << 0,
    Edge = 1 << 1,
    Face = 1 << 2,
    All = Vertex | Edge | Face,
}

// Can make extension methods for an Enum but can't just add methods to an enum normally? Shrug emoji
internal static class ComponentMaskExtensions
{
    // MeshComponentType ordinals match ComponentMask bit positions (Vertex=0 → 1<<0, …).
    public static bool Includes(this ComponentMask mask, MeshComponentType type) =>
        mask.HasFlag((ComponentMask)(1 << (int)type));
}
