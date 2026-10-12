using Godot;

public partial class Root
{
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
            return;

        if (IsEnter(key) && _application?.Tools.HasPrimaryAction == true)
        {
            if (_application.Tools.TryExecutePrimaryAction() is { } failure)
                ShowWarningToast(failure);

            GetViewport().SetInputAsHandled();
            return;
        }

        if (
            InputMap.EventIsAction(@event, EditorInputActions.EditDelete, true)
            && _application is not null
        )
        {
            if (_application.TryDeleteSelection() is { } deleteFailure)
                ShowWarningToast(deleteFailure);

            GetViewport().SetInputAsHandled();
            return;
        }

        string? toolId = ToolForInput(@event);
        if (
            toolId is null
            || _application is null
            || (
                _application.SelectionMode.Mode != EditorMode.Object
                && toolId is RotateTool.ToolId or ScaleTool.ToolId
            )
        )
        {
            return;
        }

        _viewportArea?.ActiveViewport?.StopToolDrag();
        _application.Tools.Activate(toolId);
        GetViewport().SetInputAsHandled();
    }

    private static bool IsEnter(InputEventKey key) =>
        key.Keycode is Key.Enter or Key.KpEnter
        || key.PhysicalKeycode is Key.Enter or Key.KpEnter;

    internal void ShowWarningToast(string message)
    {
        PanelContainer toast = GetNode<PanelContainer>("UI/WarningToast");
        toast.GetNode<Label>("WarningToastLabel").Text = $"\u26A0 {message}";
        toast.Show();
        toast.GetNode<Timer>("WarningToastTimer").Start();
    }

    private void HideWarningToast() => GetNode<Control>("UI/WarningToast").Hide();

    private static string? ToolForInput(InputEvent @event)
    {
        if (InputMap.EventIsAction(@event, EditorInputActions.ToolTranslate, true))
            return TranslateTool.ToolId;
        if (InputMap.EventIsAction(@event, EditorInputActions.ToolRotate, true))
            return RotateTool.ToolId;
        if (InputMap.EventIsAction(@event, EditorInputActions.ToolScale, true))
            return ScaleTool.ToolId;
        return null;
    }
}
