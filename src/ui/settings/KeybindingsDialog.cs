#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Godot;

public partial class KeybindingsDialog : Window
{
    private readonly Dictionary<string, Button> _bindingButtons = new(
        StringComparer.Ordinal
    );

    private KeybindingManager? _keybindings;
    private string? _capturingActionName;
    private (string ActionName, InputEventKey Binding)? _pendingReplacement;
    private Label? _statusLabel;
    private AppConfirmDialog? _conflictDialog;

    internal AppConfirmDialog ConflictDialog => _conflictDialog!;

    public override void _Ready()
    {
        _statusLabel = GetNode<Label>("%StatusLabel");
        _conflictDialog = GetNode<AppConfirmDialog>("%ConflictDialog");

        AddBindingButton(EditorInputActions.CameraMoveForward, "%MoveForwardButton");
        AddBindingButton(EditorInputActions.CameraMoveBackward, "%MoveBackwardButton");
        AddBindingButton(EditorInputActions.CameraMoveLeft, "%MoveLeftButton");
        AddBindingButton(EditorInputActions.CameraMoveRight, "%MoveRightButton");

        GetNode<Button>("%ResetButton").Pressed += ResetDefaults;
        GetNode<Button>("%CloseButton").Pressed += CloseDialog;
        _conflictDialog.Confirmed += ConfirmReplacement;
        _conflictDialog.Canceled += CancelReplacement;
        CloseRequested += CloseDialog;
    }

    public override void _Input(InputEvent @event)
    {
        if (
            !Visible
            || _capturingActionName is not string inputActionName
            || @event is not InputEventKey keyEvent
            || !keyEvent.Pressed
            || keyEvent.Echo
        )
        {
            return;
        }

        Key physicalKey = keyEvent.PhysicalKeycode != Key.None
            ? keyEvent.PhysicalKeycode
            : keyEvent.Keycode;
        if (physicalKey == Key.Escape)
        {
            CancelCapture();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (physicalKey == Key.None || IsModifierKey(physicalKey))
        {
            SetStatus("Enter the desired key combination.", true);
            GetViewport().SetInputAsHandled();
            return;
        }

        InputEventKey binding = new()
        {
            PhysicalKeycode = physicalKey,
            CtrlPressed = keyEvent.CtrlPressed,
            AltPressed = keyEvent.AltPressed,
            ShiftPressed = keyEvent.ShiftPressed,
            MetaPressed = keyEvent.MetaPressed,
        };
        AssignOrWarn(inputActionName, binding);
        GetViewport().SetInputAsHandled();
    }

    public override void _ExitTree()
    {
        _keybindings = null;
        _capturingActionName = null;
        ClearPendingReplacement();
        _bindingButtons.Clear();
    }

    public void ShowDialog(KeybindingManager keybindings)
    {
        ArgumentNullException.ThrowIfNull(keybindings);
        _keybindings = keybindings;
        _capturingActionName = null;
        ClearPendingReplacement();
        _conflictDialog?.Hide();
        SetStatus("Select a binding, then enter the desired key combination. Escape cancels.");
        RefreshBindings();
        PopupCentered();
    }

    internal void BeginCapture(string inputActionName)
    {
        if (_keybindings is null)
            return;

        _capturingActionName = inputActionName;
        KeybindingDefinition definition = EditorInputActions.Definitions[inputActionName];
        SetStatus($"Select a new binding for {definition.DisplayName}.");
        RefreshBindings();
    }

    private void AddBindingButton(string inputActionName, string path)
    {
        Button button = GetNode<Button>(path);
        _bindingButtons.Add(inputActionName, button);
        button.Pressed += () => BeginCapture(inputActionName);
    }

    private void AssignOrWarn(string inputActionName, InputEventKey binding)
    {
        if (_keybindings is null || _conflictDialog is null)
        {
            binding.Dispose();
            return;
        }

        KeybindingAssignmentResult result;
        try
        {
            result = _keybindings.Assign(inputActionName, binding);
        }
        catch (IOException exception)
        {
            binding.Dispose();
            _capturingActionName = null;
            SetStatus(exception.Message, true);
            RefreshBindings();
            return;
        }

        _capturingActionName = null;
        if (result.Applied)
        {
            binding.Dispose();
            SetAssignedStatus(inputActionName);
            RefreshBindings();
            return;
        }

        string conflictingActionName = result.ConflictingActionName!;
        ClearPendingReplacement();
        _pendingReplacement = (inputActionName, binding);
        string bindingText = binding.AsTextPhysicalKeycode();
        string conflictingName = EditorInputActions
            .Definitions[conflictingActionName]
            .DisplayName;
        _conflictDialog.DialogText =
            $"{bindingText} is assigned to {conflictingName}.\n\n"
            + "Replace it and leave that action unassigned?";
        RefreshBindings();
        _conflictDialog.PopupCentered();
    }

    private void ConfirmReplacement()
    {
        if (
            _keybindings is null
            || _pendingReplacement is not { } replacement
        )
        {
            return;
        }

        try
        {
            _keybindings.Assign(
                replacement.ActionName,
                replacement.Binding,
                replaceConflict: true
            );
            SetAssignedStatus(replacement.ActionName);
        }
        catch (IOException exception)
        {
            SetStatus(exception.Message, true);
        }

        replacement.Binding.Dispose();
        _pendingReplacement = null;
        RefreshBindings();
    }

    private void CancelReplacement()
    {
        ClearPendingReplacement();
        SetStatus("Binding unchanged.");
    }

    private void CloseDialog()
    {
        _capturingActionName = null;
        ClearPendingReplacement();
        _conflictDialog?.Hide();
        Hide();
    }

    private void ResetDefaults()
    {
        if (_keybindings is null)
            return;

        try
        {
            _keybindings.ResetDefaults();
            _capturingActionName = null;
            ClearPendingReplacement();
            SetStatus("Default bindings restored.");
            RefreshBindings();
        }
        catch (IOException exception)
        {
            SetStatus(exception.Message, true);
        }
    }

    private void CancelCapture()
    {
        _capturingActionName = null;
        SetStatus("Binding unchanged.");
        RefreshBindings();
    }

    private void RefreshBindings()
    {
        if (_keybindings is null)
            return;

        foreach ((string inputActionName, Button button) in _bindingButtons)
        {
            button.Text = _capturingActionName == inputActionName
                ? "Press a key..."
                : _keybindings.GetDisplayText(inputActionName);
        }
    }

    private void SetAssignedStatus(string inputActionName)
    {
        if (_keybindings is null)
            return;

        KeybindingDefinition definition = EditorInputActions.Definitions[inputActionName];
        SetStatus(
            $"{definition.DisplayName}: {_keybindings.GetDisplayText(inputActionName)}"
        );
    }

    private void SetStatus(string message, bool isError = false)
    {
        if (_statusLabel is null)
            return;

        _statusLabel.Text = message;
        _statusLabel.Modulate = isError
            ? new Color("e48373")
            : new Color("9ca3b5");
    }

    private static bool IsModifierKey(Key key) =>
        key is Key.Ctrl or Key.Alt or Key.Shift or Key.Meta;

    private void ClearPendingReplacement()
    {
        _pendingReplacement?.Binding.Dispose();
        _pendingReplacement = null;
    }
}
