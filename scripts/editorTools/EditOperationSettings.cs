using System;

public sealed class EditOperationSettings
{
    public string SelectedOperationId { get; private set; }

    public bool ExtrudeAlongFaceNormal { get; private set; } = true;

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
}
