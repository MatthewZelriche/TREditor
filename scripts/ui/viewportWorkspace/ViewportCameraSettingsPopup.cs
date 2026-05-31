using Godot;

public partial class ViewportCameraSettingsPopup : PopupPanel
{
    private ViewCamera _camera;
    private HSlider _horizontalSensitivitySlider;
    private Label _horizontalSensitivityValue;
    private HSlider _verticalSensitivitySlider;
    private Label _verticalSensitivityValue;
    private HSlider _moveSpeedSlider;
    private Label _moveSpeedValue;
    private bool _nodesCached;
    private bool _signalsWired;

    public override void _Ready()
    {
        CacheSceneNodes();
        WireSceneNodes();
        SyncFromCamera();
    }

    public void Initialize(ViewCamera camera)
    {
        _camera = camera;

        CacheSceneNodes();
        WireSceneNodes();
        SyncFromCamera();
    }

    public void ToggleBelow(Control anchor)
    {
        if (anchor == null)
        {
            return;
        }

        if (Visible)
        {
            Hide();
            return;
        }

        Vector2 popupPosition = anchor.GlobalPosition + new Vector2(0.0f, anchor.Size.Y);
        Position = new Vector2I(
            Mathf.RoundToInt(popupPosition.X),
            Mathf.RoundToInt(popupPosition.Y)
        );
        Popup();
    }

    private void CacheSceneNodes()
    {
        if (_nodesCached)
        {
            return;
        }

        _horizontalSensitivitySlider = GetNode<HSlider>(
            "Margin/Column/Controls/HorizontalSensitivitySlider"
        );
        _horizontalSensitivityValue = GetNode<Label>(
            "Margin/Column/Controls/HorizontalSensitivityValue"
        );
        _verticalSensitivitySlider = GetNode<HSlider>(
            "Margin/Column/Controls/VerticalSensitivitySlider"
        );
        _verticalSensitivityValue = GetNode<Label>(
            "Margin/Column/Controls/VerticalSensitivityValue"
        );
        _moveSpeedSlider = GetNode<HSlider>("Margin/Column/Controls/MoveSpeedSlider");
        _moveSpeedValue = GetNode<Label>("Margin/Column/Controls/MoveSpeedValue");

        _nodesCached = true;
    }

    private void WireSceneNodes()
    {
        if (_signalsWired)
        {
            return;
        }

        _horizontalSensitivitySlider.ValueChanged += OnHorizontalSensitivityChanged;
        _verticalSensitivitySlider.ValueChanged += OnVerticalSensitivityChanged;
        _moveSpeedSlider.ValueChanged += OnMoveSpeedChanged;

        _signalsWired = true;
    }

    private void SyncFromCamera()
    {
        if (_camera != null)
        {
            _horizontalSensitivitySlider.Value = _camera.HorizontalSensitivity;
            _verticalSensitivitySlider.Value = _camera.VerticalSensitivity;
            _moveSpeedSlider.Value = _camera.MoveSpeed;
        }

        UpdateSliderValue(_horizontalSensitivityValue, _horizontalSensitivitySlider.Value, "0.00");
        UpdateSliderValue(_verticalSensitivityValue, _verticalSensitivitySlider.Value, "0.00");
        UpdateSliderValue(_moveSpeedValue, _moveSpeedSlider.Value, "0");
    }

    private void OnHorizontalSensitivityChanged(double value)
    {
        if (_camera != null)
        {
            _camera.HorizontalSensitivity = (float)value;
        }

        UpdateSliderValue(_horizontalSensitivityValue, value, "0.00");
    }

    private void OnVerticalSensitivityChanged(double value)
    {
        if (_camera != null)
        {
            _camera.VerticalSensitivity = (float)value;
        }

        UpdateSliderValue(_verticalSensitivityValue, value, "0.00");
    }

    private void OnMoveSpeedChanged(double value)
    {
        if (_camera != null)
        {
            _camera.MoveSpeed = (float)value;
        }

        UpdateSliderValue(_moveSpeedValue, value, "0");
    }

    private static void UpdateSliderValue(Label label, double value, string format)
    {
        if (label != null)
        {
            label.Text = value.ToString(format);
        }
    }
}
