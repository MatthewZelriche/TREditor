// Everything a tool is allowed to reach. Document access is read-only and the only route to a
// document change is Commands; no member exposes a mutable Scene.
public interface IEditorToolContext
{
    IEditorDocumentReader Document { get; }

    IEditorCommandSink Commands { get; }

    PickingService Picking { get; }

    SelectionState Selection { get; }

    // Transform previews for drag feedback. Tools get preview state rather than a service so a
    // preview reaches rendering and picking through one notification instead of per-service calls.
    PreviewState Preview { get; }
}
