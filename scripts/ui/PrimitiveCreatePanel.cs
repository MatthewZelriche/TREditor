using Godot;

public partial class PrimitiveCreatePanel : PanelContainer
{
    private enum PrimitiveType
    {
        Box = 0,
        Cylinder = 1,
    }

    private EditorSession _session;
    private OptionButton _primitiveTypeOption;
    private Control _boxSettings;
    private Control _cylinderSettings;
    private SpinBox _cylinderSides;
    private bool _nodesCached;
    private bool _signalsWired;

    public override void _Ready()
    {
        CacheSceneNodes();
        WireSceneNodes();
        ShowPrimitiveSettings(_primitiveTypeOption.Selected);
        UpdateCreationSettings();
    }

    private void CacheSceneNodes()
    {
        if (_nodesCached)
        {
            return;
        }

        _primitiveTypeOption = GetNode<OptionButton>("Margin/Column/PrimitiveTypeOption");
        _boxSettings = GetNode<Control>("Margin/Column/Settings/BoxSettings");
        _cylinderSettings = GetNode<Control>("Margin/Column/Settings/CylinderSettings");
        _cylinderSides = GetNode<SpinBox>("Margin/Column/Settings/CylinderSettings/Sides");
        _session = GetNodeOrNull<EditorSession>("%WORLD_ROOT");

        _nodesCached = true;
    }

    private void WireSceneNodes()
    {
        if (_signalsWired)
        {
            return;
        }

        _primitiveTypeOption.ItemSelected += OnPrimitiveTypeSelected;
        _cylinderSides.ValueChanged += OnCylinderSidesChanged;

        _signalsWired = true;
    }

    private void OnPrimitiveTypeSelected(long index)
    {
        ShowPrimitiveSettings(index);
        UpdateCreationSettings();
    }

    private void OnCylinderSidesChanged(double value)
    {
        UpdateCreationSettings();
    }

    private void ShowPrimitiveSettings(long index)
    {
        _boxSettings.Visible = index == 0;
        _cylinderSettings.Visible = index == 1;
    }

    private void UpdateCreationSettings()
    {
        if (_session == null)
        {
            GD.PushWarning("PrimitiveCreatePanel could not find EditorSession.");
            return;
        }

        _session.PrimitiveCreationSettings =
            (PrimitiveType)_primitiveTypeOption.Selected == PrimitiveType.Box
                ? PrimitiveCreationSettings.Box()
                : PrimitiveCreationSettings.Cylinder(Mathf.RoundToInt((float)_cylinderSides.Value));
    }
}
