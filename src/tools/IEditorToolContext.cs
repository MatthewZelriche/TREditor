// Everything a tool is allowed to reach. Document access is read-only and the only route to a
// document change is Commands; no member exposes a mutable Document.
public interface IEditorToolContext
{
    IEditorDocumentReader Document { get; }

    IEditorCommandSink Commands { get; }

    PickingService Picking { get; }

    // Read-only selection plus ephemeral hover operations. User-visible selection changes are
    // created as commands and submitted through Commands.
    IEditorSelection Selection { get; }

    // Transform previews for drag feedback. Tools get preview state rather than a service so a
    // preview reaches rendering and picking through one notification instead of per-service calls.
    PreviewState Preview { get; }

    // Editor-owned transform gizmo hosts, reached by camera. Tools drive interaction through this
    // coordinator rather than touching viewport-local Gizmo3D nodes.
    TranslateGizmoCoordinator TranslateGizmos { get; }

    // Shared snap increments from the bottom bar. Tools read these; they do not own the settings.
    SnapSettings Snap { get; }
}
