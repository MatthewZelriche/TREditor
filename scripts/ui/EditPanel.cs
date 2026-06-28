using System.Collections.Generic;
using Godot;

public partial class EditPanel : PanelContainer
{
    private const float DefaultInsetSliderStep = 0.05f;
    private const float MinimumInsetSliderStep = 0.0001f;
    private const int OperationButtonHeight = 42;
    private const int IconPlaceholderSize = 24;

    private readonly Dictionary<string, Button> _operationButtons = [];

    private EditorSession _session;
    private GridContainer _operationGrid;
    private Label _optionsTitle;
    private Label _noOptions;
    private CheckBox _extrudeAlongFaceNormal;
    private Control _insetOptions;
    private HSlider _insetDepth;
    private Label _insetDepthValue;
    private Button _insetApply;
    private Button _insetCancel;
    private Control _bevelOptions;
    private HSlider _bevelWidth;
    private Label _bevelWidthValue;
    private Button _bevelApply;
    private Button _bevelCancel;
    private Control _collapseVerticesOptions;
    private Control _collapseVerticesTargetRow;
    private OptionButton _collapseVerticesTarget;
    private Label _collapseVerticesCentroidInfo;
    private Button _collapseVerticesApply;
    private Button _collapseVerticesCancel;
    private Control _fillHoleOptions;
    private Button _fillHoleApply;
    private Button _fillHoleCancel;
    private Control _collapseFaceOptions;
    private Button _collapseFaceApply;
    private Button _collapseFaceCancel;

    public override void _Ready()
    {
        _session = GetNodeOrNull<EditorSession>("%WORLD_ROOT");
        _operationGrid = GetNode<GridContainer>("Margin/Scroll/Column/OperationGrid");
        _optionsTitle = GetNode<Label>("Margin/Scroll/Column/OptionsTitle");
        _noOptions = GetNode<Label>("Margin/Scroll/Column/NoOptions");
        _extrudeAlongFaceNormal = GetNode<CheckBox>(
            "Margin/Scroll/Column/ExtrudeOptions/AlongFaceNormal"
        );
        _insetOptions = GetNode<Control>("Margin/Scroll/Column/InsetOptions");
        _insetDepth = GetNode<HSlider>("Margin/Scroll/Column/InsetOptions/DepthRow/Depth");
        _insetDepthValue = GetNode<Label>("Margin/Scroll/Column/InsetOptions/DepthRow/DepthValue");
        _insetApply = GetNode<Button>("Margin/Scroll/Column/InsetOptions/Actions/Apply");
        _insetCancel = GetNode<Button>("Margin/Scroll/Column/InsetOptions/Actions/Cancel");
        _bevelOptions = GetNode<Control>("Margin/Scroll/Column/BevelOptions");
        _bevelWidth = GetNode<HSlider>("Margin/Scroll/Column/BevelOptions/WidthRow/Width");
        _bevelWidthValue = GetNode<Label>("Margin/Scroll/Column/BevelOptions/WidthRow/WidthValue");
        _bevelApply = GetNode<Button>("Margin/Scroll/Column/BevelOptions/Actions/Apply");
        _bevelCancel = GetNode<Button>("Margin/Scroll/Column/BevelOptions/Actions/Cancel");
        _collapseVerticesOptions = GetNode<Control>("Margin/Scroll/Column/CollapseVerticesOptions");
        _collapseVerticesTargetRow = GetNode<Control>(
            "Margin/Scroll/Column/CollapseVerticesOptions/TargetRow"
        );
        _collapseVerticesTarget = GetNode<OptionButton>(
            "Margin/Scroll/Column/CollapseVerticesOptions/TargetRow/Target"
        );
        _collapseVerticesCentroidInfo = GetNode<Label>(
            "Margin/Scroll/Column/CollapseVerticesOptions/CentroidInfo"
        );
        _collapseVerticesApply = GetNode<Button>(
            "Margin/Scroll/Column/CollapseVerticesOptions/Actions/Apply"
        );
        _collapseVerticesCancel = GetNode<Button>(
            "Margin/Scroll/Column/CollapseVerticesOptions/Actions/Cancel"
        );
        _fillHoleOptions = GetNode<Control>("Margin/Scroll/Column/FillHoleOptions");
        _fillHoleApply = GetNode<Button>("Margin/Scroll/Column/FillHoleOptions/Actions/Apply");
        _fillHoleCancel = GetNode<Button>("Margin/Scroll/Column/FillHoleOptions/Actions/Cancel");
        _collapseFaceOptions = GetNode<Control>("Margin/Scroll/Column/CollapseFaceOptions");
        _collapseFaceApply = GetNode<Button>(
            "Margin/Scroll/Column/CollapseFaceOptions/Actions/Apply"
        );
        _collapseFaceCancel = GetNode<Button>(
            "Margin/Scroll/Column/CollapseFaceOptions/Actions/Cancel"
        );

        BuildOperationButtons();
        _collapseVerticesTarget.AddItem("First selected vertex");
        _collapseVerticesTarget.AddItem("Second selected vertex");
        _extrudeAlongFaceNormal.Toggled += OnExtrudeAlongFaceNormalToggled;
        _insetDepth.ValueChanged += OnInsetDepthChanged;
        _insetApply.Pressed += OnInsetApplyPressed;
        _insetCancel.Pressed += OnInsetCancelPressed;
        _bevelWidth.ValueChanged += OnBevelWidthChanged;
        _bevelApply.Pressed += OnApplyPressed;
        _bevelCancel.Pressed += OnCancelPressed;
        _collapseVerticesTarget.ItemSelected += OnCollapseVerticesTargetSelected;
        _collapseVerticesApply.Pressed += OnApplyPressed;
        _collapseVerticesCancel.Pressed += OnCancelPressed;
        _fillHoleApply.Pressed += OnApplyPressed;
        _fillHoleCancel.Pressed += OnCancelPressed;
        _collapseFaceApply.Pressed += OnApplyPressed;
        _collapseFaceCancel.Pressed += OnCancelPressed;
        if (_session != null)
        {
            _session.EditOperationSettings.Changed += RefreshOperationSelection;
            _session.GridSnapSizeChanged += RefreshOperationSelection;
            _session.Selection.SelectionChanged += RefreshOperationSelection;
        }
        RefreshOperationSelection();
    }

    public override void _ExitTree()
    {
        _extrudeAlongFaceNormal.Toggled -= OnExtrudeAlongFaceNormalToggled;
        _insetDepth.ValueChanged -= OnInsetDepthChanged;
        _insetApply.Pressed -= OnInsetApplyPressed;
        _insetCancel.Pressed -= OnInsetCancelPressed;
        _bevelWidth.ValueChanged -= OnBevelWidthChanged;
        _bevelApply.Pressed -= OnApplyPressed;
        _bevelCancel.Pressed -= OnCancelPressed;
        _collapseVerticesTarget.ItemSelected -= OnCollapseVerticesTargetSelected;
        _collapseVerticesApply.Pressed -= OnApplyPressed;
        _collapseVerticesCancel.Pressed -= OnCancelPressed;
        _fillHoleApply.Pressed -= OnApplyPressed;
        _fillHoleCancel.Pressed -= OnCancelPressed;
        _collapseFaceApply.Pressed -= OnApplyPressed;
        _collapseFaceCancel.Pressed -= OnCancelPressed;
        if (_session != null)
        {
            _session.EditOperationSettings.Changed -= RefreshOperationSelection;
            _session.GridSnapSizeChanged -= RefreshOperationSelection;
            _session.Selection.SelectionChanged -= RefreshOperationSelection;
        }
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
        bool insetSelected = selectedId == "InsetFace";
        bool bevelEdgeSelected = selectedId == "BevelEdge";
        bool bevelVertexSelected = selectedId == "BevelVertex";
        bool bevelSelected = bevelEdgeSelected || bevelVertexSelected;
        bool collapseVerticesSelected = selectedId == "CollapseVertices";
        bool fillHoleSelected = selectedId == "FillHole";
        bool collapseFaceSelected = selectedId == "CollapseFace";
        _optionsTitle.Text = selectedId switch
        {
            "ExtrudeFace" => "EXTRUDE FACE OPTIONS",
            "InsetFace" => "INSET FACE OPTIONS",
            "BevelEdge" => "BEVEL EDGE OPTIONS",
            "BevelVertex" => "BEVEL VERTEX OPTIONS",
            "CollapseVertices" => "COLLAPSE VERTICES OPTIONS",
            "FillHole" => "FILL HOLE OPTIONS",
            "CollapseFace" => "COLLAPSE FACE OPTIONS",
            _ => "OPTIONS",
        };
        _noOptions.Text =
            selectedId == null
                ? "Select an operation to view its settings."
                : "This operation has no settings.";
        _noOptions.Visible =
            !extrudeSelected
            && !insetSelected
            && !bevelSelected
            && !collapseVerticesSelected
            && !fillHoleSelected
            && !collapseFaceSelected;
        _extrudeAlongFaceNormal.GetParent<Control>().Visible = extrudeSelected;
        _insetOptions.Visible = insetSelected;
        _bevelOptions.Visible = bevelSelected;
        _collapseVerticesOptions.Visible = collapseVerticesSelected;
        _fillHoleOptions.Visible = fillHoleSelected;
        _collapseFaceOptions.Visible = collapseFaceSelected;
        _extrudeAlongFaceNormal.SetPressedNoSignal(
            _session?.EditOperationSettings.ExtrudeAlongFaceNormal ?? true
        );
        float insetDepth = _session?.EditOperationSettings.InsetDepth ?? 0.25f;
        float maximumInsetDepth = 0f;
        bool canInset =
            insetSelected
            && _session != null
            && _session.TryGetMaximumSelectedFaceInsetDepth(out maximumInsetDepth);
        if (canInset)
        {
            float gridSnapSize = _session.GridSnapSize;
            float step =
                gridSnapSize > GridSnap.Off
                    ? Mathf.Min(gridSnapSize, maximumInsetDepth)
                    : Mathf.Max(
                        MinimumInsetSliderStep,
                        Mathf.Min(DefaultInsetSliderStep, maximumInsetDepth / 100f)
                    );
            _insetDepth.MinValue = step;
            _insetDepth.MaxValue = maximumInsetDepth;
            _insetDepth.Step = step;
            insetDepth = _session.GetSnappedInsetDepth();
        }
        _insetDepth.SetValueNoSignal(insetDepth);
        _insetDepthValue.Text = FormatInsetDepth(insetDepth);
        _insetApply.Disabled = !canInset;
        float bevelWidth = _session?.EditOperationSettings.BevelWidth ?? 0.25f;
        float maximumBevelWidth = 0f;
        bool canBevel =
            bevelSelected
            && _session != null
            && _session.TryGetMaximumSelectedBevelWidth(out maximumBevelWidth);
        if (canBevel)
        {
            float gridSnapSize = _session.GridSnapSize;
            float step =
                gridSnapSize > GridSnap.Off
                    ? Mathf.Min(gridSnapSize, maximumBevelWidth)
                    : Mathf.Max(
                        MinimumInsetSliderStep,
                        Mathf.Min(DefaultInsetSliderStep, maximumBevelWidth / 100f)
                    );
            _bevelWidth.MinValue = step;
            _bevelWidth.MaxValue = maximumBevelWidth;
            _bevelWidth.Step = step;
            bevelWidth = _session.GetSnappedBevelWidth();
        }
        _bevelWidth.SetValueNoSignal(bevelWidth);
        _bevelWidthValue.Text = FormatInsetDepth(bevelWidth);
        _bevelApply.Disabled = !canBevel;
        int selectedVertexCount =
            collapseVerticesSelected && _session != null ? _session.Selection.Current.Count : 0;
        _collapseVerticesTargetRow.Visible = selectedVertexCount == 2;
        _collapseVerticesCentroidInfo.Visible = selectedVertexCount > 2;
        _collapseVerticesTarget.Select(
            (int)(
                _session?.EditOperationSettings.TwoVertexCollapseTarget
                ?? CollapseVerticesTarget.First
            )
        );
        _collapseVerticesApply.Disabled = !(_session?.CanApplySelectedEditOperation() ?? false);
        _fillHoleApply.Disabled = !(_session?.CanApplySelectedEditOperation() ?? false);
        _collapseFaceApply.Disabled = !(_session?.CanApplySelectedEditOperation() ?? false);
    }

    private void OnExtrudeAlongFaceNormalToggled(bool enabled)
    {
        _session?.EditOperationSettings.SetExtrudeAlongFaceNormal(enabled);
    }

    private void OnInsetDepthChanged(double depth)
    {
        if (_session == null)
            return;

        float snappedDepth = _session.TryGetMaximumSelectedFaceInsetDepth(out float maximumDepth)
            ? GridSnap.SnapDistance((float)depth, _session.GridSnapSize, maximumDepth)
            : (float)depth;
        _session.EditOperationSettings.SetInsetDepth(snappedDepth);
    }

    private void OnBevelWidthChanged(double width)
    {
        if (_session == null)
            return;

        float snappedWidth = _session.TryGetMaximumSelectedBevelWidth(out float maximumWidth)
            ? GridSnap.SnapDistance((float)width, _session.GridSnapSize, maximumWidth)
            : (float)width;
        _session.EditOperationSettings.SetBevelWidth(snappedWidth);
    }

    private void OnCollapseVerticesTargetSelected(long index)
    {
        if (_session == null || !System.Enum.IsDefined(typeof(CollapseVerticesTarget), (int)index))
            return;

        _session.EditOperationSettings.SetTwoVertexCollapseTarget((CollapseVerticesTarget)index);
    }

    private void OnInsetApplyPressed()
    {
        OnApplyPressed();
    }

    private void OnInsetCancelPressed()
    {
        OnCancelPressed();
    }

    private void OnApplyPressed()
    {
        _session?.ApplySelectedEditOperation();
    }

    private void OnCancelPressed()
    {
        _session?.CancelSelectedEditOperation();
    }

    internal static string BuildTooltip(EditOperationDefinition operation) =>
        $"{operation.Description}\nSelection: {operation.Selection}\nInput: {operation.Input}";

    internal static string FormatInsetDepth(float depth) =>
        depth >= 0.01f ? depth.ToString("0.00") : depth.ToString("0.0000");
}
