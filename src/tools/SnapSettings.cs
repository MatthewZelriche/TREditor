using System;

// Editor-wide snap increments shared by the bottom-bar UI and tools. Null means snapping is off.
public sealed class SnapSettings
{
    public float? GridSnap { get; private set; } = 16.0f;

    public float? AngleSnap { get; private set; }

    public event Action? Changed;

    public void SetGridSnap(float? value)
    {
        if (GridSnap == value)
            return;

        GridSnap = value;
        Changed?.Invoke();
    }

    public void SetAngleSnap(float? value)
    {
        if (AngleSnap == value)
            return;

        AngleSnap = value;
        Changed?.Invoke();
    }
}
