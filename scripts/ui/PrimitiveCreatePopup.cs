using Godot;

public partial class PrimitiveCreatePopup : PopupPanel
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
        _createButton = GetNode<Button>("Margin/Column/Actions/CreateButton");
        _session = GetNodeOrNull<EditorSession>("%WORLD_ROOT");

        _nodesCached = true;
    }

    private void WireSceneNodes()
    {
        if (_signalsWired)
        {
            return;
        }

        _primitiveTypeOption.ItemSelected += ShowPrimitiveSettings;
        _createButton.Pressed += OnCreatePressed;

        _signalsWired = true;
    }

    private void ShowPrimitiveSettings(long index)
    {
        _boxSettings.Visible = index == 0;
        _cylinderSettings.Visible = index == 1;
    }

    private void OnCreatePressed()
    {
        if (_session == null)
        {
            GD.PushWarning("PrimitiveCreatePopup could not find EditorSession.");
            return;
        }

        PrimitiveType primitiveType = (PrimitiveType)_primitiveTypeOption.Selected;
        BeginInteractiveCreation(primitiveType);
        Hide();
    }

    private void BeginInteractiveCreation(PrimitiveType primitiveType)
    {
        switch (primitiveType)
        {
            case PrimitiveType.Box:
                _session.BeginPrimitiveCreation(PrimitiveCreationSettings.Box());
                break;
            case PrimitiveType.Cylinder:
                _session.BeginPrimitiveCreation(
                    PrimitiveCreationSettings.Cylinder(
                        GetInt("Margin/Column/Settings/CylinderSettings/Sides")
                    )
                );
                break;
        }
    }

    private int GetInt(NodePath spinBoxPath) =>
        Mathf.RoundToInt((float)GetNode<SpinBox>(spinBoxPath).Value);
}
