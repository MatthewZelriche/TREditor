using Godot;

public partial class PrimitiveCreatePopup : PopupPanel
{
    private OptionButton _primitiveTypeOption;
    private Control _boxSettings;
    private Control _cylinderSettings;
    private Control _sphereSettings;
    private Control _planeSettings;
    private Control _coneSettings;
    private Button _createButton;
    private bool _nodesCached;
    private bool _signalsWired;

    public override void _Ready()
    {
        CacheSceneNodes();
        WireSceneNodes();
        ShowPrimitiveSettings(_primitiveTypeOption.Selected);
    }

    public void ToggleBeside(Control anchor)
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

        Vector2 popupPosition = anchor.GlobalPosition + new Vector2(anchor.Size.X, 0.0f);
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

        _primitiveTypeOption = GetNode<OptionButton>("Margin/Column/PrimitiveTypeOption");
        _boxSettings = GetNode<Control>("Margin/Column/Settings/BoxSettings");
        _cylinderSettings = GetNode<Control>("Margin/Column/Settings/CylinderSettings");
        _sphereSettings = GetNode<Control>("Margin/Column/Settings/SphereSettings");
        _planeSettings = GetNode<Control>("Margin/Column/Settings/PlaneSettings");
        _coneSettings = GetNode<Control>("Margin/Column/Settings/ConeSettings");
        _createButton = GetNode<Button>("Margin/Column/Actions/CreateButton");

        _nodesCached = true;
    }

    private void WireSceneNodes()
    {
        if (_signalsWired)
        {
            return;
        }

        _primitiveTypeOption.ItemSelected += ShowPrimitiveSettings;
        _createButton.Pressed += Hide;

        _signalsWired = true;
    }

    private void ShowPrimitiveSettings(long index)
    {
        _boxSettings.Visible = index == 0;
        _cylinderSettings.Visible = index == 1;
        _sphereSettings.Visible = index == 2;
        _planeSettings.Visible = index == 3;
        _coneSettings.Visible = index == 4;
    }
}
