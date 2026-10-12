using System;
using Godot;

public partial class PrimitiveToolSettings : VBoxContainer
{
    private const string ScenePath = "res://src/ui/toolsettings/PrimitiveToolSettings.tscn";

    private PrimitiveTool _tool = null!;
    private OptionButton _shapeOption = null!;
    private HBoxContainer _sidesRow = null!;
    private SpinBox _sidesSpin = null!;
    private bool _syncing;

    public static PrimitiveToolSettings Create(PrimitiveTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        PackedScene scene =
            GD.Load<PackedScene>(ScenePath)
            ?? throw new InvalidOperationException($"Failed to load '{ScenePath}'.");
        PrimitiveToolSettings control = scene.Instantiate<PrimitiveToolSettings>();
        control.Bind(tool);
        return control;
    }

    private void Bind(PrimitiveTool tool)
    {
        _tool = tool;
        _shapeOption = GetNode<OptionButton>("%ShapeOption");
        _sidesRow = GetNode<HBoxContainer>("%SidesRow");
        _sidesSpin = GetNode<SpinBox>("%SidesSpin");
        _sidesSpin.MinValue = PrimitiveTool.MinimumCylinderSides;
        _sidesSpin.MaxValue = PrimitiveTool.MaximumCylinderSides;
        _tool.SettingsChanged += Sync;
        Sync();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete && _tool is not null)
            _tool.SettingsChanged -= Sync;
    }

    private void Sync()
    {
        _syncing = true;
        _shapeOption.Select((int)_tool.Shape);
        _sidesSpin.Value = _tool.CylinderSides;
        _sidesRow.Visible = _tool.Shape == PrimitiveShape.Cylinder;
        _syncing = false;
    }

    // Wired from .tscn.
    private void OnShapeSelected(long index)
    {
        if (!_syncing)
            _tool.Shape = (PrimitiveShape)_shapeOption.GetItemId((int)index);
    }

    // Wired from .tscn.
    private void OnSidesChanged(double value)
    {
        if (!_syncing)
            _tool.CylinderSides = Mathf.RoundToInt(value);
    }
}
