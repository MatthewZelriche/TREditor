using System.Collections.Generic;
using Godot;

public partial class EditPanel : PanelContainer
{
    private const int OperationButtonHeight = 42;
    private const int IconPlaceholderSize = 24;

    private readonly Dictionary<string, Button> _operationButtons = [];

    private EditorSession _session;
    private GridContainer _operationGrid;
    private Label _optionsTitle;
    private Label _noOptions;
    private CheckBox _extrudeAlongFaceNormal;

    public override void _Ready()
    {
        _session = GetNodeOrNull<EditorSession>("%WORLD_ROOT");
        _operationGrid = GetNode<GridContainer>("Margin/Scroll/Column/OperationGrid");
        _optionsTitle = GetNode<Label>("Margin/Scroll/Column/OptionsTitle");
        _noOptions = GetNode<Label>("Margin/Scroll/Column/NoOptions");
        _extrudeAlongFaceNormal = GetNode<CheckBox>(
            "Margin/Scroll/Column/ExtrudeOptions/AlongFaceNormal"
        );

        BuildOperationButtons();
        _extrudeAlongFaceNormal.Toggled += OnExtrudeAlongFaceNormalToggled;
        RefreshOperationSelection();
    }

    public override void _ExitTree()
    {
        _extrudeAlongFaceNormal.Toggled -= OnExtrudeAlongFaceNormalToggled;
    }

    private void BuildOperationButtons()
    {
        foreach (EditOperationDefinition operation in EditOperationCatalog.All)
        {
            Button button = CreateOperationButton(operation);
            _operationGrid.AddChild(button);
            _operationButtons.Add(operation.Id, button);
        }
    }

    private Button CreateOperationButton(EditOperationDefinition operation)
    {
        Button button = new()
        {
            CustomMinimumSize = new Vector2(0, OperationButtonHeight),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ToggleMode = true,
            TooltipText = BuildTooltip(operation),
        };
        button.Pressed += () => ToggleOperation(operation, button.ButtonPressed);

        HBoxContainer content = new() { MouseFilter = MouseFilterEnum.Ignore };
        content.AddThemeConstantOverride("separation", 7);
        button.AddChild(content);
        content.SetAnchorsPreset(LayoutPreset.FullRect);
        content.OffsetLeft = 7;
        content.OffsetTop = 5;
        content.OffsetRight = -12;
        content.OffsetBottom = -5;

        PanelContainer iconPlaceholder = new()
        {
            CustomMinimumSize = new Vector2(IconPlaceholderSize, IconPlaceholderSize),
            MouseFilter = MouseFilterEnum.Ignore,
            TooltipText = "Operation icon placeholder",
        };
        content.AddChild(iconPlaceholder);

        Label name = new()
        {
            Text = operation.DisplayName,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        if (operation.Availability != EditOperationAvailability.Available)
            name.Modulate = new Color(0.72f, 0.72f, 0.72f);
        content.AddChild(name);

        return button;
    }

    private void ToggleOperation(EditOperationDefinition operation, bool selected)
    {
        if (_session == null)
            return;

        if (selected)
            _session.EditOperationSettings.Select(operation.Id);
        else
            _session.EditOperationSettings.Deselect();
        RefreshOperationSelection();
    }

    private void RefreshOperationSelection()
    {
        string selectedId = _session?.EditOperationSettings.SelectedOperationId;
        foreach ((string id, Button button) in _operationButtons)
            button.SetPressedNoSignal(id == selectedId);

        bool extrudeSelected = selectedId == "ExtrudeFace";
        _optionsTitle.Text = extrudeSelected ? "EXTRUDE FACE OPTIONS" : "OPTIONS";
        _noOptions.Text =
            selectedId == null
                ? "Select an operation to view its settings."
                : "This operation has no settings.";
        _noOptions.Visible = !extrudeSelected;
        _extrudeAlongFaceNormal.GetParent<Control>().Visible = extrudeSelected;
        _extrudeAlongFaceNormal.SetPressedNoSignal(
            _session?.EditOperationSettings.ExtrudeAlongFaceNormal ?? true
        );
    }

    private void OnExtrudeAlongFaceNormalToggled(bool enabled)
    {
        _session?.EditOperationSettings.SetExtrudeAlongFaceNormal(enabled);
    }

    internal static string BuildTooltip(EditOperationDefinition operation) =>
        $"{operation.Description}\nSelection: {operation.Selection}\nInput: {operation.Input}";
}
