#nullable enable

using System;
using System.Collections.Generic;
using Godot;

public partial class KeybindingsDialog : Window
{
    private sealed record BindingRow(Button BindingButton);

    private sealed record PendingChange(string ActionId, InputBinding? Binding, bool IsReset);

    private readonly Dictionary<string, BindingRow> _rows = new(StringComparer.Ordinal);

    private VBoxContainer? _categories;
    private Button? _cancelCaptureButton;
    private ConfirmationDialog? _replaceConfirmation;
    private ConfirmationDialog? _restoreConfirmation;
    private AcceptDialog? _errorDialog;
    private KeybindingService? _service;
    private string? _capturingActionId;
    private PendingChange? _pendingChange;

    public override void _Ready()
    {
        Title = "Keybindings";
        Size = new Vector2I(760, 640);
        MinSize = new Vector2I(620, 420);
        Unresizable = false;

        _categories = GetNode<VBoxContainer>("Margin/Content/Scroll/Categories");
        _cancelCaptureButton = GetNode<Button>("Margin/Content/Footer/CancelCapture");
        _replaceConfirmation = GetNode<ConfirmationDialog>("ReplaceConfirmation");
        _restoreConfirmation = GetNode<ConfirmationDialog>("RestoreConfirmation");
        _errorDialog = GetNode<AcceptDialog>("ErrorDialog");

        Button restoreButton = GetNode<Button>("Margin/Content/Footer/RestoreDefaults");
        Button closeButton = GetNode<Button>("Margin/Content/Footer/Close");

        CloseRequested += OnCloseRequested;
        closeButton.Pressed += OnCloseRequested;
        restoreButton.Pressed += OnRestoreDefaultsPressed;
        _cancelCaptureButton.Pressed += CancelCapture;
        _replaceConfirmation.Confirmed += OnReplaceConfirmed;
        _replaceConfirmation.Canceled += ClearPendingChange;
        _restoreConfirmation.Confirmed += OnRestoreDefaultsConfirmed;

        _service = KeybindingService.Instance;
        if (_service == null)
        {
            ShowError("The keybinding service is not available.");
            return;
        }

        _service.BindingChanged += OnBindingChanged;
        PopulateRows();
    }

    public override void _ExitTree()
    {
        if (_service != null)
            _service.BindingChanged -= OnBindingChanged;
    }

    public override void _Input(InputEvent @event)
    {
        if (_capturingActionId == null)
            return;

        bool isCapturableInput =
            @event is InputEventKey
            || @event is InputEventMouseButton { ButtonIndex: not MouseButton.Left };
        if (!isCapturableInput)
            return;

        GetViewport().SetInputAsHandled();
        if (!InputBinding.TryCapture(@event, out InputBinding? binding))
            return;

        string actionId = _capturingActionId;
        CancelCapture();
        TryApplyChange(new PendingChange(actionId, binding, IsReset: false));
    }

    private void PopulateRows()
    {
        string? currentCategory = null;
        GridContainer? grid = null;
        foreach (KeybindingDefinition definition in KeybindingCatalog.All)
        {
            if (definition.Category != currentCategory)
            {
                currentCategory = definition.Category;
                Label heading = new()
                {
                    Text = currentCategory,
                    ThemeTypeVariation = "HeaderMedium",
                };
                _categories!.AddChild(heading);

                grid = new GridContainer { Columns = 4 };
                grid.AddThemeConstantOverride("h_separation", 8);
                grid.AddThemeConstantOverride("v_separation", 6);
                _categories.AddChild(grid);
            }

            Label actionLabel = new()
            {
                Text = definition.DisplayName,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            Button bindingButton = new()
            {
                CustomMinimumSize = new Vector2(210, 0),
                TooltipText = "Click, then press a key or mouse button.",
            };
            Button clearButton = new() { Text = "Clear" };
            Button resetButton = new() { Text = "Reset" };

            string actionId = definition.ActionId;
            bindingButton.Pressed += () => BeginCapture(actionId);
            clearButton.Pressed += () =>
                TryApplyChange(new PendingChange(actionId, null, IsReset: false));
            resetButton.Pressed += () =>
                TryApplyChange(new PendingChange(actionId, null, IsReset: true));

            grid!.AddChild(actionLabel);
            grid.AddChild(bindingButton);
            grid.AddChild(clearButton);
            grid.AddChild(resetButton);

            _rows.Add(actionId, new BindingRow(bindingButton));
            RefreshRow(actionId);
        }
    }

    private void BeginCapture(string actionId)
    {
        CancelCapture();
        _capturingActionId = actionId;
        _rows[actionId].BindingButton.Text = "Press a key or mouse button…";
        _cancelCaptureButton!.Visible = true;
    }

    private void CancelCapture()
    {
        string? previousActionId = _capturingActionId;
        _capturingActionId = null;
        if (previousActionId != null)
            RefreshRow(previousActionId);

        if (_cancelCaptureButton != null)
            _cancelCaptureButton.Visible = false;
    }

    private void TryApplyChange(PendingChange change, bool replaceConflict = false)
    {
        if (_service == null)
            return;

        KeybindingChangeResult result = change.IsReset
            ? _service.ResetBinding(change.ActionId, replaceConflict)
            : _service.SetBinding(change.ActionId, change.Binding, replaceConflict);
        switch (result.Status)
        {
            case KeybindingChangeStatus.Applied:
                _pendingChange = null;
                break;
            case KeybindingChangeStatus.Conflict:
                ShowConflict(change, result.ConflictingActionId!);
                break;
            case KeybindingChangeStatus.PersistenceFailed:
                _pendingChange = null;
                ShowError(result.Error ?? "Unable to save the keybinding.");
                break;
        }
    }

    private void ShowConflict(PendingChange change, string conflictingActionId)
    {
        _pendingChange = change;
        string conflictingName = KeybindingCatalog.Get(conflictingActionId).DisplayName;
        _replaceConfirmation!.DialogText =
            $"This binding is already assigned to “{conflictingName}”.\n\n"
            + "Move it to the selected action?";
        _replaceConfirmation.PopupCentered();
    }

    private void OnReplaceConfirmed()
    {
        PendingChange? pending = _pendingChange;
        _pendingChange = null;
        if (pending != null)
            TryApplyChange(pending, replaceConflict: true);
    }

    private void ClearPendingChange() => _pendingChange = null;

    private void OnRestoreDefaultsPressed()
    {
        CancelCapture();
        _restoreConfirmation!.PopupCentered();
    }

    private void OnRestoreDefaultsConfirmed()
    {
        if (_service == null)
            return;

        KeybindingChangeResult result = _service.ResetAll();
        if (!result.WasApplied)
            ShowError(result.Error ?? "Unable to restore the default keybindings.");
    }

    private void OnBindingChanged(string actionId) => RefreshRow(actionId);

    private void RefreshRow(string actionId)
    {
        if (
            _service == null
            || !_rows.TryGetValue(actionId, out BindingRow? row)
            || _capturingActionId == actionId
        )
        {
            return;
        }

        row.BindingButton.Text = _service.GetBindingDisplayText(actionId);
    }

    private void ShowError(string message)
    {
        GD.PushError(message);
        _errorDialog!.DialogText = message;
        _errorDialog.PopupCentered();
    }

    private void OnCloseRequested()
    {
        CancelCapture();
        Hide();
    }
}
