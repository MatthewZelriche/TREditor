using System;

public sealed class EditOperationSettings
{
    public string SelectedOperationId { get; private set; }

    public bool ExtrudeAlongFaceNormal { get; private set; } = true;

    public float InsetDepth { get; private set; } = 0.25f;

    public float BevelWidth { get; private set; } = 0.25f;

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
}
