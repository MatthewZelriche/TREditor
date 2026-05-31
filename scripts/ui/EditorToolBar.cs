using Godot;

public partial class EditorToolBar : PanelContainer
{
    private Button _createButton;
    private PrimitiveCreatePopup _primitiveCreatePopup;

    public override void _Ready()
    {
        _createButton = GetNode<Button>("VBoxContainer/Create");
        _primitiveCreatePopup = GetNode<PrimitiveCreatePopup>("PrimitiveCreatePopup");

        _createButton.Pressed += OnCreatePressed;
    }

    private void OnCreatePressed()
    {
        _primitiveCreatePopup.ToggleBeside(_createButton);
    }
}
