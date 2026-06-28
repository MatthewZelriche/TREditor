using System;

public enum CollapseVerticesTarget
{
    First,
    Second,
}

public sealed class EditOperationSettings
{
    public string SelectedOperationId { get; private set; }

    public bool ExtrudeAlongFaceNormal { get; private set; } = true;

    public float InsetDepth { get; private set; } = 0.25f;

    public float BevelWidth { get; private set; } = 0.25f;

    public CollapseVerticesTarget TwoVertexCollapseTarget { get; private set; } =
        CollapseVerticesTarget.First;

    public int BridgeSegments { get; private set; } = 4;

    public float BridgeArchAngleDegrees { get; private set; } = 180f;

    public event Action Changed;

    public bool IsSelected(string operationId) => SelectedOperationId == operationId;

    public void Select(string operationId)
    {
        if (SelectedOperationId == operationId)
            return;

        SelectedOperationId = operationId;
        Changed?.Invoke();
    }

    public void Deselect()
    {
        if (SelectedOperationId == null)
            return;

        SelectedOperationId = null;
        Changed?.Invoke();
    }

    public void SetExtrudeAlongFaceNormal(bool enabled)
    {
        if (ExtrudeAlongFaceNormal == enabled)
            return;

        ExtrudeAlongFaceNormal = enabled;
        Changed?.Invoke();
    }

    public void SetInsetDepth(float depth)
    {
        if (!(depth > 0f) || !float.IsFinite(depth) || InsetDepth == depth)
            return;

        InsetDepth = depth;
        Changed?.Invoke();
    }

    public void SetBevelWidth(float width)
    {
        if (!(width > 0f) || !float.IsFinite(width) || BevelWidth == width)
            return;

        BevelWidth = width;
        Changed?.Invoke();
    }

    public void SetTwoVertexCollapseTarget(CollapseVerticesTarget target)
    {
        if (TwoVertexCollapseTarget == target)
            return;

        TwoVertexCollapseTarget = target;
        Changed?.Invoke();
    }

    public void SetBridgeSegments(int segments)
    {
        if (segments < 1 || BridgeSegments == segments)
            return;

        BridgeSegments = segments;
        Changed?.Invoke();
    }

    public void SetBridgeArchAngleDegrees(float angle)
    {
        if (angle < 0f || angle > 180f || !float.IsFinite(angle) || BridgeArchAngleDegrees == angle)
        {
            return;
        }

        BridgeArchAngleDegrees = angle;
        Changed?.Invoke();
    }
}
