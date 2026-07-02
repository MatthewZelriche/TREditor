using Godot;

public static class ObjectPickResolver
{
    public static ScenePickHit ResolveCandidate(
        EditorObjectId objectId,
        ScenePickHit vertex,
        ScenePickHit edge,
        ScenePickHit face
    )
    {
        ScenePickHit closest = ScenePickHit.None;
        UpdateClosest(vertex, ref closest);
        UpdateClosest(edge, ref closest);
        UpdateClosest(face, ref closest);

        return closest.HasHit
            ? ScenePickHit.ObjectHit(objectId, closest.Position, closest.Distance)
            : ScenePickHit.None;
    }

    private static void UpdateClosest(ScenePickHit candidate, ref ScenePickHit closest)
    {
        if (candidate.HasHit && (!closest.HasHit || candidate.Distance < closest.Distance))
        {
            closest = candidate;
        }
    }
}
