using System;
using System.Collections.Generic;
using Godot;

/// <summary>Appends open-ended tube geometry to caller-owned render buffers.</summary>
public static class TubeMeshBuilder
{
    public static bool Append(
        Vector3 start,
        Vector3 end,
        float radius,
        int segments,
        List<Vector3> vertices,
        List<Vector3> normals,
        List<int> indices,
        bool reverseWinding = false
    )
    {
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(normals);
        ArgumentNullException.ThrowIfNull(indices);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        if (segments < 3)
            throw new ArgumentOutOfRangeException(nameof(segments), "A tube needs three segments.");

        Vector3 axis = end - start;
        if (axis.IsZeroApprox())
            return false;

        axis = axis.Normalized();
        Vector3 reference = Mathf.Abs(axis.Dot(Vector3.Up)) > 0.95f ? Vector3.Right : Vector3.Up;
        Vector3 sideA = axis.Cross(reference).Normalized();
        Vector3 sideB = axis.Cross(sideA).Normalized();
        int firstIndex = vertices.Count;

        for (int index = 0; index < segments; index++)
        {
            float angle = Mathf.Tau * index / segments;
            Vector3 normal = sideA * Mathf.Cos(angle) + sideB * Mathf.Sin(angle);
            vertices.Add(start + normal * radius);
            normals.Add(normal);
            vertices.Add(end + normal * radius);
            normals.Add(normal);
        }

        for (int index = 0; index < segments; index++)
        {
            int next = (index + 1) % segments;
            int a = firstIndex + index * 2;
            int b = firstIndex + next * 2;
            int c = firstIndex + next * 2 + 1;
            int d = firstIndex + index * 2 + 1;

            if (reverseWinding)
            {
                indices.Add(a);
                indices.Add(c);
                indices.Add(b);
                indices.Add(a);
                indices.Add(d);
                indices.Add(c);
            }
            else
            {
                indices.Add(a);
                indices.Add(b);
                indices.Add(c);
                indices.Add(a);
                indices.Add(c);
                indices.Add(d);
            }
        }

        return true;
    }
}
