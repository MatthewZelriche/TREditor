using Godot;

internal static class GeometryUtils
{
    public static bool TryIntersectPlane(
        Camera3D camera,
        Vector2 screenPosition,
        Vector3 planePoint,
        Vector3 planeNormal,
        out Vector3 intersection
    )
    {
        Vector3 origin = camera.ProjectRayOrigin(screenPosition);
        Vector3 direction = camera.ProjectRayNormal(screenPosition);
        float denominator = planeNormal.Dot(direction);
        if (Mathf.IsZeroApprox(denominator))
        {
            intersection = default;
            return false;
        }

        float distance = planeNormal.Dot(planePoint - origin) / denominator;
        intersection = origin + (direction * distance);
        return distance >= 0.0f && float.IsFinite(distance) && intersection.IsFinite();
    }
}
