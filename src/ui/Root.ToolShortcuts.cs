using Godot;

public partial class Root
{
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false })
            return;

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
