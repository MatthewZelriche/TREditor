using Godot;

public readonly record struct RaySegmentClosestPoints(
    float RayDistance,
    float SegmentParameter,
    Vector3 RayPoint,
    Vector3 SegmentPoint
);

/// <summary>Shared geometric queries involving a ray and a finite line segment.</summary>
public static class RaySegmentGeometry
{
    private const float Epsilon = 0.000001f;

    /// <summary>
    /// Finds the closest points between a ray and a segment.
    /// </summary>
    /// <remarks>
    /// <paramref name="rayDirection"/> must be normalized. The returned ray distance and segment
    /// parameter are clamped to the ray's non-negative half-line and the segment's [0, 1] range.
    /// </remarks>
    public static RaySegmentClosestPoints FindClosestPoints(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        Vector3 segmentStart,
        Vector3 segmentEnd
    )
    {
        Vector3 segmentDirection = segmentEnd - segmentStart;
        Vector3 offset = rayOrigin - segmentStart;
        float raySegmentDot = rayDirection.Dot(segmentDirection);
        float segmentLengthSquared = segmentDirection.LengthSquared();
        float rayOffsetDot = rayDirection.Dot(offset);
        float segmentOffsetDot = segmentDirection.Dot(offset);
        float denominator = segmentLengthSquared - raySegmentDot * raySegmentDot;

        float rayDistance;
        float segmentParameter;
        if (segmentLengthSquared <= Epsilon)
        {
            rayDistance = Mathf.Max(0f, -rayOffsetDot);
            segmentParameter = 0f;
        }
        else if (denominator > Epsilon)
        {
            rayDistance =
                (raySegmentDot * segmentOffsetDot - segmentLengthSquared * rayOffsetDot)
                / denominator;
            segmentParameter = (segmentOffsetDot - raySegmentDot * rayOffsetDot) / denominator;
            if (rayDistance < 0f)
            {
                rayDistance = 0f;
                segmentParameter = segmentOffsetDot / segmentLengthSquared;
            }

            segmentParameter = Mathf.Clamp(segmentParameter, 0f, 1f);
            Vector3 segmentPoint = segmentStart + segmentDirection * segmentParameter;
            rayDistance = Mathf.Max(0f, (segmentPoint - rayOrigin).Dot(rayDirection));
            segmentParameter = Mathf.Clamp(
                (rayOrigin + rayDirection * rayDistance - segmentStart).Dot(segmentDirection)
                    / segmentLengthSquared,
                0f,
                1f
            );
        }
        else
        {
            rayDistance = Mathf.Max(0f, -rayOffsetDot);
            segmentParameter = Mathf.Clamp(segmentOffsetDot / segmentLengthSquared, 0f, 1f);
        }

        return new RaySegmentClosestPoints(
            rayDistance,
            segmentParameter,
            rayOrigin + rayDirection * rayDistance,
            segmentStart + segmentDirection * segmentParameter
        );
    }
}
