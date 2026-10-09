using Godot;

// Optional capability for tools that have sidebar settings. The caller owns the returned control
// and frees it when the tool stops being active; the tool owns its contents and behavior.
public interface IEditorToolSettingsProvider
{
    Control CreateSettingsControl(IEditorToolContext context);
}
